/** Only the local launcher can announce a generated public tunnel to the server. */
export function publicInviteOrigin(value:unknown):string|null{
 if(typeof value!=='string')return null;
 try{const url=new URL(value);return url.protocol==='https:'&&!url.username&&!url.password&&!url.port&&/^[-a-z0-9]+\.trycloudflare\.com$/.test(url.hostname)&&url.pathname==='/'&&!url.search&&!url.hash?url.origin:null;}catch{return null;}
}
export function roomInviteUrl(page:string,code:string,publicOrigin?:unknown){
 const url=new URL(publicInviteOrigin(publicOrigin)||page);url.search='';url.hash='';url.searchParams.set('room',code);return url.toString();
}
