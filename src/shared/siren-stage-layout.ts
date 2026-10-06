/**
 * Siren stage ("Ward Zero", a sealed asylum hall): collision layout, the hall's shared reference points (columns, add
 * spawns, chip spots) and the beat-synced stage hazard schedule. Owned by the visuals branch
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
export const SIREN_STAGE_THEME={name:'Ward Zero',tag:'HAZARD · floor vents open into black pits',theme:'siren',sky:0x040306,floor:0x2a2731,accent:0xe9e4f2} as const;

/**
 * The hall: a sealed asylum atrium the Siren has taken over. Interior faces at x = ±halfX and z = ±halfZ (the global
 * 64 x 56 ARENA footprint, floor top at y = 0) and a solid roof whose underside is at y = roof. Walls reach the roof,
 * so no jump chip (about 9.8 m apex) or launch (about 12.5 m) can leave the room, while Black Rain (spawns 8 m above
 * the ground) and lobbed Damnation (about 12 m apex) still fit under it.
 */
export const SIREN_HALL={halfX:32,halfZ:28,roof:16} as const;
/** Structural columns along the walls (also the nearest cover at the perimeter). Shared so the dressing matches. */
export const SIREN_COLUMNS:{x:number;z:number}[]=[
 ...[-19,-4,4,19].flatMap(z=>[-1,1].map(s=>({x:s*30.6,z}))),
 ...[-24,-12,12,24].flatMap(x=>[-1,1].map(s=>({x,z:s*26.6}))),
 ...[-1,1].flatMap(sx=>[-1,1].map(sz=>({x:sx*30.6,z:sz*26.6}))),
];
/**
 * Where adds will come from (not spawned yet): the four isolation cells and four ward doors, each with the yaw that
 * faces into the hall. Every point is clear floor (tests check capsule clearance) and away from the player pads.
 */
export const SIREN_ADD_SPAWNS:{id:string;kind:'cell'|'door';x:number;z:number;yaw:number}[]=[
 ...[-1,1].flatMap(s=>[-11,11].map(z=>({id:`cell-${s<0?'w':'e'}${z<0?'n':'s'}`,kind:'cell' as const,x:s*27,z,yaw:s<0?Math.PI/2:-Math.PI/2}))),
 ...[-1,1].flatMap(sz=>[-18,18].map(x=>({id:`door-${sz<0?'n':'s'}${x<0?'w':'e'}`,kind:'door' as const,x,z:sz*25.4,yaw:sz<0?0:Math.PI}))),
];
/** Pads for chips that players will run to grab (not active yet): on the outer running loop, clear of vents and cover. */
export const SIREN_CHIP_SPOTS:{id:string;x:number;z:number}[]=[[-18,-4],[18,4],[-7,-21],[7,21],[19,-19],[-19,19]].map(([x,z],i)=>({id:'chip-'+i,x,z}));

export function sirenStageBlocks():ArenaBlock[]{
 const out:ArenaBlock[]=[];let serial=0;const H=SIREN_HALL;
 const b=(kind:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,material:Material='foundation',extra:Partial<ArenaBlock>={})=>{out.push({id:`siren-${kind}-${++serial}`,pos:{x,y,z},size:{x:sx,y:sy,z:sz},material,...extra});};
 // Floor, walls that reach the roof, and the roof itself: a sealed building with no ring-outs and no way over.
 b('floor',0,-.75,0,64,1.5,56);
 for(const s of [-1,1]){b('wall',s*(H.halfX+.5),H.roof/2,0,1,H.roof,2*H.halfZ+2);b('wall',0,H.roof/2,s*(H.halfZ+.5),2*H.halfX,H.roof,1);}
 b('roof',0,H.roof+.5,0,2*H.halfX+2,1,2*H.halfZ+2);
 // The colonnade: indestructible columns from floor to vault around the walls.
 for(const c of SIREN_COLUMNS)b('column',c.x,H.roof/2,c.z,1.4,H.roof,1.4);
 // Four restraint pillars around the centre, chained to the vault: the nearest indestructible cover from the middle.
 for(const x of [-6,6])for(const z of [-6,6])b('pylon',x,H.roof/2,z,1.5,H.roof,1.5);
 // Mid ring of broken partition walls (indestructible), long faces toward the centre.
 for(const s of [-1,1]){
  b('partition',s*14,1.5,0,1,3,6);
  b('partition',0,1.5,s*12.5,7,3,1);
  // Corner L-shapes between the partitions.
  b('partition',s*13,1.5,s*12,5,3,1);b('partition',s*15,1.5,s*10,1,3,4);
  b('partition',-s*13,1.5,s*12,5,3,1);b('partition',-s*15,1.5,s*10,1,3,4);
 }
 // Isolation cells along the long walls: three-sided, open toward the centre. Adds will be let out of these.
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
  // Cabinets dragged out of the cells and left by the cell fronts (the cells stay clear for adds).
  for(const z of [-15.5,15.5])crate(s*23,z);
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
