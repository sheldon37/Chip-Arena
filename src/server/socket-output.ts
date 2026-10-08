import {WebSocket} from 'ws';

/** Bound the compression work and never queue a trail of obsolete world states. */
export const SOCKET_COMPRESSION={threshold:1024,serverNoContextTakeover:true,clientNoContextTakeover:true,concurrencyLimit:2,zlibDeflateOptions:{level:1,memLevel:7}};
export class SocketOutput {
 private clients=new WeakMap<WebSocket,{pending:boolean;dropped:number}>();
 private state(ws:WebSocket){let s=this.clients.get(ws);if(!s){s={pending:false,dropped:0};this.clients.set(ws,s);}return s;}
 diagnostics(ws:WebSocket){const s=this.state(ws);return {bufferedBytes:ws.bufferedAmount,droppedSnapshots:s.dropped,compression:ws.extensions.includes('permessage-deflate')};}
 send(ws:WebSocket,packet:string,snapshot=false){
  if(ws.readyState!==WebSocket.OPEN)return false;
  const state=this.state(ws);
  if(snapshot){
   if(state.pending||ws.bufferedAmount>64*1024){state.dropped++;return false;}
   state.pending=true;
   ws.send(packet,{compress:true},()=>{state.pending=false;});
  }else{
   // Control messages remain ordered/reliable; a dead connection cannot grow forever.
   if(ws.bufferedAmount>256*1024){ws.close(1013,'Connection is too slow. Rejoin the room.');return false;}
   ws.send(packet,{compress:false});
  }
  return true;
 }
}
