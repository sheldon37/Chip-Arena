type RoomAddress={endpoint:string;invite:string;code?:string};
declare global {interface Window {arenaDesktop?:{openUpdater:()=>Promise<unknown>;lanOpen?:(id:number,url:string)=>Promise<boolean>;lanSend?:(id:number,data:string)=>void;lanClose?:(id:number)=>void;onLanEvent?:(callback:(value:any)=>void)=>()=>void;hostRoom?:(online:boolean)=>Promise<RoomAddress>;leaveRoom?:()=>Promise<boolean>;resolveRoom?:(value:string)=>Promise<RoomAddress>;onStatus?:(callback:(text:string)=>void)=>()=>void}}}
export function hasDesktopUpdater(){return typeof window.arenaDesktop?.openUpdater==='function';}
export function desktopHosting(){return typeof window.arenaDesktop?.hostRoom==='function';}
export async function openDesktopUpdater(){return window.arenaDesktop?.openUpdater();}
