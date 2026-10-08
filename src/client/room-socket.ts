import './desktop-updates.js';
export interface RoomSocket{readonly url:string;bufferedAmount:number;readyState:number;onopen:((e:any)=>void)|null;onmessage:((e:any)=>void)|null;onerror:((e:any)=>void)|null;onclose:((e:any)=>void)|null;send(data:string):void;close():void;}
let serial=0;
class DesktopLanSocket implements RoomSocket {
 bufferedAmount=0;readyState=0;onopen:RoomSocket['onopen']=null;onmessage:RoomSocket['onmessage']=null;onerror:RoomSocket['onerror']=null;onclose:RoomSocket['onclose']=null;private id=++serial;private off:()=>void;
 constructor(readonly url:string){const api=window.arenaDesktop!;this.off=api.onLanEvent!(event=>{if(event.id!==this.id)return;this.bufferedAmount=event.bufferedAmount||0;if(event.type==='open'){this.readyState=1;this.onopen?.({});}else if(event.type==='message')this.onmessage?.({data:event.data});else if(event.type==='error')this.onerror?.({});else if(event.type==='close')this.finish(event.code,event.reason);});void api.lanOpen!(this.id,url).catch(()=>{this.onerror?.({});this.finish(1006,'Could not connect to LAN server');});}
 send(data:string){if(this.readyState===1)window.arenaDesktop!.lanSend!(this.id,data);}
 close(){if(this.readyState===3)return;window.arenaDesktop!.lanClose!(this.id);this.finish(1000,'Left room');}
 private finish(code:number,reason:string){if(this.readyState===3)return;this.readyState=3;this.off();this.onclose?.({code,reason});}
}
export function roomSocket(url:URL):RoomSocket{return url.protocol==='ws:'&&window.arenaDesktop?.lanOpen?new DesktopLanSocket(url.toString()):new WebSocket(url);}
