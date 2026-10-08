const {gameUrl}=require('./policy.cjs');
/** Local game sockets only; insecure LAN transport never executes content in the renderer. */
function lanEndpoint(raw){if(typeof raw!=='string')throw Error('Invalid game address.');const u=new URL(raw);if(u.protocol!=='ws:'||u.pathname!=='/socket'||u.search||u.hash)throw Error('Invalid LAN game address.');gameUrl(raw.replace(/^ws:/,'http:'));return u.toString();}
class LanSocket {
 constructor(emit,Socket=WebSocket){this.emit=emit;this.Socket=Socket;this.current=null;}
 open(id,raw){if(!Number.isSafeInteger(id)||id<1)throw Error('Invalid connection.');const url=lanEndpoint(raw);this.close();const socket=new this.Socket(url),entry=this.current={id,socket};const emit=data=>{if(this.current===entry)this.emit({id,bufferedAmount:socket.bufferedAmount,...data});};socket.addEventListener('open',()=>emit({type:'open'}));socket.addEventListener('message',e=>{if(typeof e.data==='string'&&e.data.length<=2000000)emit({type:'message',data:e.data});else socket.close(1009,'Snapshot too large');});socket.addEventListener('close',e=>emit({type:'close',code:e.code,reason:e.reason}));socket.addEventListener('error',()=>emit({type:'error'}));return true;}
 send(id,data){const c=this.current;if(c?.id!==id||typeof data!=='string'||data.length>24000||c.socket.readyState!==1)return false;c.socket.send(data);return true;}
 close(id){if(id!==undefined&&this.current?.id!==id)return;const c=this.current;this.current=null;try{c?.socket.close(1000,'Left room');}catch{}}
}
module.exports={lanEndpoint,LanSocket};
