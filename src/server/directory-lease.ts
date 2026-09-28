import {randomBytes} from 'node:crypto';
import {DIRECTORY_ORIGIN,DIRECTORY_TTL_MS} from '../shared/room-directory.js';
/** Independent, bounded background requests. Never await this from the simulation clock. */
export class DirectoryLease {
 private token=randomBytes(32).toString('hex');private pending=false;private nextAttempt=0;
 readyUntil=0;
 constructor(readonly code:string,private request:typeof fetch=fetch,private endpoint=DIRECTORY_ORIGIN){}
 get ready(){return Date.now()<this.readyUntil;}
 async refresh(origin:string|null){
  if(!origin||this.pending||Date.now()<this.nextAttempt)return;
  this.pending=true;this.nextAttempt=Date.now()+10000;
  try{
   const response=await this.request(`${this.endpoint}/api/directory/${this.code}`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:`Bearer ${this.token}`},body:JSON.stringify({origin}),redirect:'error',signal:AbortSignal.timeout(4000)});
   if(!response.ok)throw Error('Directory HTTP '+response.status);
   const result=await response.json();if(result.code!==this.code||!Number.isFinite(result.expires))throw Error('Invalid directory response');
   // Use our own clock, leaving a margin for expiry and delayed responses.
   this.readyUntil=Date.now()+DIRECTORY_TTL_MS-15000;this.nextAttempt=Date.now()+45000;
  }catch{/* Keep the ordinary invite working if discovery is temporarily unavailable. */}
  finally{this.pending=false;}
 }
}
