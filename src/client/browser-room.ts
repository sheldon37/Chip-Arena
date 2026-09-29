import type {PilotProfile} from '../shared/host-room.js';
import {PartyRoom} from './party-room.js';
/** The room creator simulates; the website relays inputs and snapshots over HTTPS. */
export class BrowserRoom {
 private onPageHide=()=>this.close();
 worker:Worker|null=null;relay:PartyRoom;closed=false;code:string;
 constructor(readonly host:boolean,code:string,profile:PilotProfile,bothDraft:boolean,receive:(m:any)=>void,status:(label:string)=>void,ended:(reason:string)=>void,draftChoices=6,skipIntros=false,selectionSeconds?:number,settings:{gameMode?:unknown;friendlyFire?:unknown;wins?:unknown;roundsPerSet?:unknown;randomMapEachSet?:unknown;map?:unknown;botCount?:unknown;botDifficulty?:unknown}={}){
 this.relay=new PartyRoom({host,code,profile,receive:(message,id)=>{if(host)this.worker?.postMessage({type:'message',id,message});else receive(message);},status,ended:reason=>{this.worker?.terminate();this.closed=true;ended(reason);},guest:(p,id)=>this.worker?.postMessage({type:'join',profile:p,id}),guestGone:id=>this.worker?.postMessage({type:'disconnect',id})});this.code=this.relay.code;window.addEventListener('pagehide',this.onPageHide);
 // Register the room before presenting its invite. Start the worker only after
 // registration succeeds so clients cannot share a nonexistent room.
 void this.relay.start().then(()=>{if(this.relay.closed||this.closed)return;if(host){this.worker=new Worker(new URL('./host-worker.ts',import.meta.url),{type:'module'});this.worker.onmessage=e=>{const {target,message}=e.data;if(target==='host'||target==='all')receive(message);if(target!=='host')this.relay.send(message,target);};this.worker.onerror=()=>{this.close();ended('Host simulation stopped. Create a new room.');};this.worker.postMessage({type:'create',code:this.code,profile,bothDraft,draftChoices,skipIntros,selectionSeconds,settings});}});
 }
 send(message:any){if(this.closed)return;if(this.host)this.worker?.postMessage({type:'message',id:'host',message});else this.relay.send(message);}
 close(){if(this.closed)return;this.closed=true;window.removeEventListener('pagehide',this.onPageHide);this.worker?.terminate();this.relay.close();}
}
