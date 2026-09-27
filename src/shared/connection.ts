/** Runtime routing lets players share invites without entering server addresses. */
export function socketEndpoint(value:unknown,origin:string):string|null {
 if(typeof value!=='string'||!value)return null;
 try{const base=new URL(origin),url=new URL(value,origin);if(url.username||url.password)return null;if(value.startsWith('/'))url.protocol=base.protocol==='https:'?'wss:':'ws:';
 if(!['ws:','wss:'].includes(url.protocol)||base.protocol==='https:'&&url.protocol!=='wss:')return null;return url.toString();}catch{return null;}
}
