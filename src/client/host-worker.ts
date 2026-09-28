import {verifyPlayerToken} from '../shared/account-auth.js';
import {initPhysics} from '../shared/simulation.js';import {HostedRoom} from '../shared/host-room.js';import {CONFIG} from '../shared/config.js';
let room:HostedRoom|null=null,clock:ReturnType<typeof setInterval>|undefined;const ready=initPhysics();
self.onmessage=async event=>{await ready;const m=event.data;if(m.type==='create'){room?.dispose();if(clock)clearInterval(clock);room=new HostedRoom(m.code,m.profile,m.bothDraft,(target,message)=>self.postMessage({target,message}),m.draftChoices,await verifyPlayerToken(m.profile?.authToken),m.skipIntros===true,m.selectionSeconds,m.settings);room.welcome('host');let previous=performance.now(),acc=0;clock=setInterval(()=>{const now=performance.now();acc+=Math.min(.1,(now-previous)/1000);previous=now;while(acc>=1/CONFIG.simulationHz){room!.step();acc-=1/CONFIG.simulationHz;}},4);}
 else if(m.type==='join'){if(!room?.join(m.profile,await verifyPlayerToken(m.profile?.authToken),m.id))self.postMessage({target:m.id,message:{type:'error',fatal:true,message:'Room is full or the match has started.'}});}
 else if(m.type==='message')room?.receive(m.id,m.message);
 else if(m.type==='disconnect')room?.disconnect(m.id);
};
