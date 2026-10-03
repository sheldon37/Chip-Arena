const {spawn}=require('node:child_process');
const {join}=require('node:path');
const {readFile}=require('node:fs/promises');
const {readFileSync,copyFileSync,rmSync,existsSync}=require('node:fs');
function validLauncherSender(event,home){return !!home&&!home.isDestroyed()&&event.sender===home.webContents&&event.senderFrame===home.webContents.mainFrame&&event.senderFrame?.url==='arena://launcher/index.html';}
async function installedVersion(root){try{const release=JSON.parse(await readFile(join(root,'release.json'),'utf8'));return typeof release.version==='string'?release.version.slice(0,80):'Unknown';}catch{return 'Unknown';}}
const LAUNCHER_FILES=['launcher/update.mjs','launcher/start.mjs','launcher/manual-update.mjs','launcher/remote-feed.mjs','launcher/local-feed.mjs'];
/** Stable rollback outside the self-updating launcher: restores the files saved before the last launcher activation. */
function rollbackLauncher(root){const record=join(root,'runtime','launcher-probation.json');try{const saved=JSON.parse(readFileSync(record,'utf8'));for(const path of Array.isArray(saved.files)?saved.files:[]){if(!LAUNCHER_FILES.includes(path))continue;const backup=join(root,'runtime','launcher-backup',path),live=join(root,path);if(existsSync(backup))copyFileSync(backup,live);else if(Array.isArray(saved.absent)&&saved.absent.includes(path))rmSync(live,{force:true});}rmSync(record,{force:true});rmSync(join(root,'runtime','launcher-backup'),{recursive:true,force:true});return true;}catch{return false;}}
/** Only a fixed bundled updater can run; no renderer-supplied paths, URLs or arguments. */
function runUpdate(root,{spawnImpl=spawn,onProgress=()=>{},timeoutMs=90000,maxMs=45*60000}={}){return new Promise((resolve,reject)=>{
 let process,buffer='',result,error='',finished=false,total=0,spoke=false,timer;
 const finish=(failure)=>{if(finished)return;finished=true;clearTimeout(timer);clearTimeout(limit);failure?reject(failure):resolve(result);};
 const expire=()=>{finish(Error('Update timed out. Reopen the launcher to retry.'));process?.kill();};
 // Large downloads report progress; the timeout only fires after timeoutMs without any updater output.
 const idle=()=>{clearTimeout(timer);timer=setTimeout(expire,timeoutMs);};idle();const limit=setTimeout(expire,maxMs);
 try{process=spawnImpl(join(root,'runtime','node.exe'),[join(root,'launcher','manual-update.mjs')],{cwd:root,windowsHide:true,shell:false,stdio:['ignore','pipe','pipe']});}catch(e){finish(e);return;}
 process.stdout.on('data',chunk=>{if(finished)return;total+=chunk.length;if(total>65536){process.kill();finish(Error('Unexpected updater response.'));return;}buffer+=chunk.toString();idle();let end;while((end=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,end);buffer=buffer.slice(end+1);try{const value=JSON.parse(line);if(['progress','error','result'].includes(value?.type))spoke=true;if(value.type==='progress'&&typeof value.text==='string')onProgress(value.text.slice(0,500));else if(value.type==='error'&&typeof value.text==='string')error=value.text.slice(0,500);else if(value.type==='result'&&['installed','current'].includes(value.status)&&typeof value.version==='string'&&/^[\w.-]{1,80}$/.test(value.version))result={status:value.status,version:value.version};}catch{/* Non-protocol output never counts as a successful update. */}}});
 process.stderr.on('data',chunk=>{error=(error+chunk.toString()).slice(-500);});process.once('error',finish);process.once('close',code=>{if(code===0&&result)return finish(null);
  // A newly activated launcher that dies before speaking the protocol is rolled back by this stable code.
  if(!spoke&&existsSync(join(root,'runtime','launcher-probation.json'))&&rollbackLauncher(root))return finish(Error('The updated launcher could not start, so the previous launcher was restored. Try Update again.'));
  finish(Error(error||'Update failed. Your existing game is available; try again.'));});
 });}
class ManualUpdater{
 constructor(actions){this.actions=actions;this.busy=false;this.pending=null;}
 start(){if(this.busy||this.actions.blocked())return Promise.reject(Error('Wait for the current operation to finish.'));this.busy=true;
 this.pending=(async()=>{try{if(this.actions.hasSession()&&!await this.actions.confirm())return {status:'cancelled'};if(this.actions.closing())return {status:'cancelled'};await this.actions.stop();this.actions.closeGame();return await this.actions.run();}finally{this.busy=false;}})();return this.pending;
 }
}
module.exports={validLauncherSender,installedVersion,runUpdate,rollbackLauncher,ManualUpdater};
