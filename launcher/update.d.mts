export const UPDATE_BASE:string;
export function allowedPath(path:unknown):boolean;
export function applyUpdates(root:string,options?:{fetchImpl?:(url:string,options?:RequestInit)=>Promise<Response>;force?:boolean;strict?:boolean;log?:(message:string)=>void}):Promise<boolean>;
