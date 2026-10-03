// Offline TEST BOSS room (design §7.1, D31): the authoritative HostedRoom runs in the host worker with `local:true` (no relay,
// no guests, pause supported); the render thread only receives its 20 Hz snapshots. Mirrors BrowserRoom's surface so main.ts
// treats it like any hosted room (send, close, host, closed).
import type {PilotProfile} from '../../shared/host-room.js';

/** The worker surface LocalRoom uses (a real Worker, or a fake in tests). */
export interface WorkerLike{postMessage(message:unknown):void;terminate():void;onmessage:((e:{data:any})=>void)|null;onerror:((e:unknown)=>void)|null}
export const LOCAL_ROOM_CODE='LOCALPVE';
export interface LocalRoomSettings{pve:{boss:'siren';skipWaves?:boolean;partyBots?:number};local:true}
const defaultWorker=():WorkerLike=>new Worker(new URL('../host-worker.ts',import.meta.url),{type:'module'}) as unknown as WorkerLike;
export class LocalRoom{
 readonly host=true;readonly code=LOCAL_ROOM_CODE;closed=false;paused=false;worker:WorkerLike|null;
 constructor(profile:PilotProfile,settings:LocalRoomSettings,receive:(m:any)=>void,status:(label:string)=>void,private ended:(reason:string)=>void,
  options:{skipIntros?:boolean;draftChoices?:number;selectionSeconds?:number;worker?:()=>WorkerLike}={}){
  this.worker=(options.worker??defaultWorker)();
  this.worker.onmessage=e=>{if(this.closed)return;const {target,message}=e.data||{};if(target==='host'||target==='all')receive(message);};
  this.worker.onerror=()=>{if(this.closed)return;this.close();ended('Boss test simulation stopped. Return to the menu and start again.');};
  this.worker.postMessage({type:'create',code:LOCAL_ROOM_CODE,profile,bothDraft:false,draftChoices:options.draftChoices??6,skipIntros:options.skipIntros===true,selectionSeconds:options.selectionSeconds,settings});
  status('LOCAL BOSS TEST');
 }
 /** While paused the worker steps nothing, so fight inputs are not sent: queued there they would be consumed one per tick
  * after resuming, behind the client's live inputs, leaving a permanent backlog (unacknowledged predictions, shots fired
  * from a stale position, rubberbanding). */
 send(message:any){if(this.closed||this.paused&&message?.type==='inputs')return;this.worker?.postMessage({type:'message',id:'host',message});}
 /** Pauses the worker's clock (the game menu in an offline run). */
 pause(paused:boolean){if(this.closed||this.paused===paused)return;this.paused=paused;this.worker?.postMessage({type:'pause',paused});}
 close(){if(this.closed)return;this.closed=true;this.worker?.terminate();this.worker=null;}
}
