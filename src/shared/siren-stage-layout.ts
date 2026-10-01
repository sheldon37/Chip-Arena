/**
 * Siren stage ("Ward Zero"): collision layout and the beat-synced stage hazard schedule. Owned by the visuals branch
 * (stage design); the systems branch registers the map, routes arenaBlocks('siren') here and applies hazard damage.
 *
 * Design rules the moveset depends on:
 *  - Indestructible cover ('foundation') within a few metres of every open area, so a Marked target can always break
 *    line of sight from the slow seeker. The seeker burns through every destructible ('wood' / 'stone' / 'metal') prop.
 *  - Destructible cover in between, so Hellfire/Damnation and the seeker have things to break.
 *  - The eight standard spawn pads (playerSpawn) stay clear.
 * Pure data: deterministic, identical on every peer.
 */
import type {ArenaBlock} from './arena.js';
import type {Material,Vec} from './config.js';
import {BLACK_SIREN_PROTOCOL,barAt,loopedSongTime,sectionAt} from './boss-music.js';

/** Suggested MAPS entry values (maps.ts is edited by the systems branch). */
export const SIREN_STAGE_THEME={name:'Ward Zero',tag:'BOSS · Black Siren Protocol',theme:'siren',sky:0x040306,floor:0x2a2731,accent:0xe9e4f2} as const;

export function sirenStageBlocks():ArenaBlock[]{
 const out:ArenaBlock[]=[];let serial=0;
 const b=(kind:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,material:Material='foundation',extra:Partial<ArenaBlock>={})=>{out.push({id:`siren-${kind}-${++serial}`,pos:{x,y,z},size:{x:sx,y:sy,z:sz},material,...extra});};
 // Floor and an enclosing ward wall: no ring-outs in the boss room.
 b('floor',0,-.75,0,64,1.5,56);
 for(const s of [-1,1]){b('wall',s*32.5,2.5,0,1,5,58);b('wall',0,2.5,s*28.5,64,5,1);}
 // Four containment pylons around the centre: the nearest indestructible cover from the middle of the room.
 for(const x of [-6,6])for(const z of [-6,6])b('pylon',x,2.5,z,1.5,5,1.5);
 // Mid ring of broken partition walls (indestructible), long faces toward the centre.
 for(const s of [-1,1]){
  b('partition',s*14,1.5,0,1,3,6);
  b('partition',0,1.5,s*12.5,7,3,1);
  // Corner L-shapes between the partitions.
  b('partition',s*13,1.5,s*12,5,3,1);b('partition',s*15,1.5,s*10,1,3,4);
  b('partition',-s*13,1.5,s*12,5,3,1);b('partition',-s*15,1.5,s*10,1,3,4);
 }
 // Cell blocks along the long walls: three-sided cells open toward the centre (cover near the side spawns).
 for(const s of [-1,1])for(const z of [-11,11]){
  b('cell',s*27,1.75,z-3,6,3.5,.6);b('cell',s*27,1.75,z+3,6,3.5,.6);b('cell',s*29.7,1.75,z,.6,3.5,6.6);
 }
 // Destructible ward furniture: cabinets (wood), gurneys (metal), rubble (stone).
 const crate=(x:number,z:number)=>b('cabinet',x,.6,z,1.2,1.2,1.2,'wood',{dynamic:true,mass:10});
 for(const s of [-1,1]){
  crate(s*9,s*-3);crate(s*9.8,s*-1.6);crate(s*3.5,s*9.5);
  b('gurney',s*19,.55,s*-6,2.4,1.1,1.1,'metal',{dynamic:true,mass:40});
  b('gurney',s*-19,.55,s*-17,2.4,1.1,1.1,'metal',{dynamic:true,mass:40});
  b('rubble',s*8,.9,s*17,2.6,1.8,1.4,'stone');b('rubble',s*21,.9,s*5,1.6,1.8,2.6,'stone');
  for(const z of [-11,11])crate(s*26.4,z);
 }
 return out;
}

/** A beat-synced floor hazard. warning: 0-1 telegraph progress (0 = none). Damage applies while active. */
export interface SirenStageHazard {id:string;kind:'vent';pos:Vec;size:Vec;warning:number;active:boolean;damage:number;launch:number;}
/** The beat-synced floor vents (id, position, which alternating group fires on a bar). */
export const SIREN_VENTS:{id:string;x:number;z:number;group:0|1}[]=[[-10,-9],[10,9],[-10,9],[10,-9],[0,-17],[0,17],[-21,-12],[21,12]].map(([x,z],i)=>({id:'vent-'+i,x,z,group:(i%2) as 0|1}));
/**
 * Floor vents erupt with black flame on the downbeat during the 'lift' and 'climax' sections, alternating groups every
 * bar. Each warns from beat 3 of the bar before (two beats of telegraph) and burns for the first beat of its bar.
 * songTime is raw song time (sim.time - bossSong.startedAt); the loop is applied here.
 */
export function sirenStageHazards(songTime:number):SirenStageHazard[]{
 const track=BLACK_SIREN_PROTOCOL,t=loopedSongTime(track,songTime),pos=barAt(track,t);if(pos.bar<1)return [];
 const live=(s:string)=>s==='lift'||s==='climax',now=sectionAt(track,t),out:SirenStageHazard[]=[];
 // Next bar's section decides whether this bar's telegraph shows (the last looped bar is followed by the loop start).
 const nextIndex=track.firstDownbeat+pos.bar*4,nextT=pos.bar===track.loop.toBar?track.loop.start:track.beats[nextIndex]??Infinity,next=nextT===Infinity?now:sectionAt(track,nextT);
 for(const v of SIREN_VENTS){
  const active=live(now)&&pos.beat===1&&(pos.bar%2)===v.group;
  const warning=live(next)&&pos.beat>=3&&((pos.bar+1)%2)===v.group?((pos.beat-3)+pos.phase)/2:0;
  out.push({id:v.id,kind:'vent',pos:{x:v.x,y:0,z:v.z},size:{x:2.4,y:2.2,z:2.4},warning,active,damage:14,launch:6.5});
 }
 return out;
}
