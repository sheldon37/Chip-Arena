const fs=require('node:fs');
const {dirname}=require('node:path');
const AUTH_ORIGIN='https://chip-arena-prototype.sheldon37.chatgpt.site';
const actions=new Set(['me','login','register','recover','profile','players','grant','logout']);
function validAccountSender(event,win,origin){if(!win||win.isDestroyed()||event.sender!==win.webContents||event.senderFrame!==win.webContents.mainFrame)return false;try{return new URL(event.senderFrame.url).origin===origin;}catch{return false;}}
/** Main-process broker: never exposes the long-lived credential to hosted game pages. */
class DesktopAccounts {
 constructor(path,safeStorage,fetch){this.path=path;this.crypto=safeStorage;this.fetch=fetch;this.session='';this.loaded=false;this.queue=Promise.resolve();}
 clear(){this.session='';this.loaded=true;fs.rmSync(this.path,{force:true});}
 canEncrypt(){return this.crypto.isEncryptionAvailable()&&(!this.crypto.getSelectedStorageBackend||this.crypto.getSelectedStorageBackend()!=='basic_text');}
 load(){if(this.loaded)return;this.loaded=true;try{if(!this.canEncrypt())return;const saved=JSON.parse(this.crypto.decryptString(fs.readFileSync(this.path)));if(/^[-\w]{43}$/.test(saved.session)&&saved.expires>Date.now())this.session=saved.session;else this.clear();}catch{/* No saved login on this device, or it belongs to a different OS user. */}}
 request(action,body){const task=()=>this.perform(action,body);const result=this.queue.then(task,task);this.queue=result.catch(()=>{});return result;}
 async perform(action,body){
  if(!actions.has(action))return {ok:false,status:400,data:{error:'Unknown account action.'}};
  const authenticating=['login','register','recover'].includes(action);
  try{
   if(body!==undefined&&(!body||typeof body!=='object'||Array.isArray(body)||JSON.stringify(body).length>8192))return {ok:false,status:400,data:{error:'Invalid account request.'}};
   this.load();if(authenticating&&body?.remember&&!this.canEncrypt())return {ok:false,status:503,data:{error:'Secure login storage is unavailable. Uncheck Remember me to sign in for this session.'}};
   if(!authenticating&&!this.session){if(action==='logout')this.clear();return {ok:false,status:401,data:{error:'Please log in.'}};}
   const response=await this.fetch(AUTH_ORIGIN+'/api/accounts/'+action,{method:action==='me'?'GET':'POST',credentials:'omit',redirect:'error',cache:'no-store',signal:AbortSignal.timeout(action==='me'?4500:20000),headers:{'Content-Type':'application/json',...(!authenticating&&this.session?{Authorization:'Bearer '+this.session}:{})},...(action==='me'?{}:{body:JSON.stringify(body||{})})});
   const data=await response.json();
   if(response.ok&&authenticating){if(!/^[-\w]{43}$/.test(data.session)||!Number.isFinite(data.expires))throw Error('Invalid account response.');this.session=data.session;fs.rmSync(this.path,{force:true});if(body.remember){fs.mkdirSync(dirname(this.path),{recursive:true});fs.writeFileSync(this.path,this.crypto.encryptString(JSON.stringify({session:this.session,expires:data.expires})),{mode:0o600});}data.desktopSession=true;delete data.session;}
   if(response.status===401&&!authenticating)this.clear();
   // No renderer API can retrieve, export, redirect or arbitrarily forward the saved session.
   delete data.session;return {ok:response.ok,status:response.status,data};
  }catch{return {ok:false,status:503,data:{error:'Account service or saved login is unavailable. Try again shortly.'}};}
  finally{if(action==='logout')this.clear();}
 }
}
module.exports={DesktopAccounts,validAccountSender,AUTH_ORIGIN};
