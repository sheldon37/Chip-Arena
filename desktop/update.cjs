const {spawn}=require('node:child_process');
const {join}=require('node:path');
const {readFile}=require('node:fs/promises');
const {readFileSync,writeFileSync,renameSync,copyFileSync,rmSync,existsSync}=require('node:fs');
function validLauncherSender(event,home){return !!home&&!home.isDestroyed()&&event.sender===home.webContents&&event.senderFrame===home.webContents.mainFrame&&event.senderFrame?.url==='arena://launcher/index.html';}
async function installedVersion(root){try{const release=JSON.parse(await readFile(join(root,'release.json'),'utf8'));return typeof release.version==='string'?release.version.slice(0,80):'Unknown';}catch{return 'Unknown';}}
const LAUNCHER_FILES=['launcher/update.mjs','launcher/start.mjs','launcher/manual-update.mjs','launcher/remote-feed.mjs','launcher/local-feed.mjs'];
/*
 * Launcher probation. This file ships only in the seed ZIP and is never self-updated, so it decides whether a new
 * launcher stays. launcher/update.mjs swaps a staged launcher in at the next start and records
 * runtime/launcher-probation.json {token, files, absent} with the previous files in runtime/launcher-backup.
 * Each kind of run the new files take part in must then succeed once in a later process: 'update' (an Update that
 * installs a version) for update.mjs, manual-update.mjs, remote-feed.mjs and local-feed.mjs, and 'start' (hosting
 * that reaches "YOUR GAME:") for start.mjs and update.mjs. Until a kind is proven, any failed run of it restores the
 * previous launcher. When every kind is proven, the backup is dropped.
 */
const runtimeFile=(root,name)=>join(root,'runtime',name);
function readRecord(root,name){try{const record=JSON.parse(readFileSync(runtimeFile(root,name),'utf8'));return record&&typeof record==='object'?record:null;}catch{return null;}}
function launcherKinds(files){const kinds=new Set();for(const path of Array.isArray(files)?files:[]){if(path!=='launcher/start.mjs')kinds.add('update');if(path==='launcher/start.mjs'||path==='launcher/update.mjs')kinds.add('start');}return kinds.size?[...kinds]:['update','start'];}
/** Restore the files saved before the last activation (journal first, then probation) and drop anything staged with them. */
function rollbackLauncher(root){
 const records=['launcher-journal.json','launcher-probation.json'].map(name=>[name,readRecord(root,name)]).filter(([,record])=>record);if(!records.length)return false;
 try{
  for(const [,record] of records)for(const path of Array.isArray(record.files)?record.files:[]){if(!LAUNCHER_FILES.includes(path))continue;const backup=join(root,'runtime','launcher-backup',path),live=join(root,path);if(existsSync(backup))copyFileSync(backup,live);else if(Array.isArray(record.absent)&&record.absent.includes(path))rmSync(live,{force:true});}
  for(const [name] of records)rmSync(runtimeFile(root,name),{force:true});
  // The feed still lists the rejected files; a later Update stages them again unless a fixed version is published.
  for(const name of ['launcher-pending','launcher-backup'])rmSync(runtimeFile(root,name),{recursive:true,force:true});
  return true;
 }catch{return false;}
}
/** Token of the launcher on probation before a run starts. A run that activates a newer launcher changes it. */
function probationToken(root){const record=readRecord(root,'launcher-probation.json');return typeof record?.token==='string'?record.token:null;}
/** A run of `kind` succeeded with the probation launcher from start to end (same token before and after). */
function proveLauncher(root,kind,token){
 const record=readRecord(root,'launcher-probation.json');if(!token||record?.token!==token||existsSync(runtimeFile(root,'launcher-journal.json')))return false;
 const proven=new Set([...(Array.isArray(record.proven)?record.proven:[]),kind]);
 try{
  if(launcherKinds(record.files).every(k=>proven.has(k))){rmSync(runtimeFile(root,'launcher-probation.json'),{force:true});rmSync(runtimeFile(root,'launcher-backup'),{recursive:true,force:true});}
  else{const temp=runtimeFile(root,'launcher-probation.tmp');writeFileSync(temp,JSON.stringify({...record,proven:[...proven]}));renameSync(temp,runtimeFile(root,'launcher-probation.json'));}
  return true;
 }catch{return false;}
}
/** A failed run of `kind` restores the previous launcher while that kind is unproven, or after a crash mid-swap. */
function restoreLauncher(root,kind){
 if(existsSync(runtimeFile(root,'launcher-journal.json')))return rollbackLauncher(root);
 const record=readRecord(root,'launcher-probation.json');
 if(!record||!launcherKinds(record.files).includes(kind)||Array.isArray(record.proven)&&record.proven.includes(kind))return false;
 return rollbackLauncher(root);
}
/** Hosting (desktop/main.cjs) around launcher/start.mjs: proven at "YOUR GAME:", restored if it fails before that. */
function hostingProbation(root){const token=probationToken(root);return {ready:()=>proveLauncher(root,'start',token),failed:()=>restoreLauncher(root,'start')};}
const sentence=text=>String(text).slice(0,300).replace(/[\s.]+$/,'')+'.';
const RESTORED=' The launcher files from the last update did not work, so the previous launcher was restored. Press Update again.';
/** Only a fixed bundled updater can run; no renderer-supplied paths, URLs or arguments. */
function runUpdate(root,{spawnImpl=spawn,onProgress=()=>{},timeoutMs=90000,maxMs=45*60000}={}){return new Promise((resolve,reject)=>{
 let child,buffer='',result,error='',failure=null,finished=false,total=0,timer,grace;const token=probationToken(root);
 const finish=e=>{if(finished)return;finished=true;clearTimeout(timer);clearTimeout(limit);clearTimeout(grace);e?reject(e):resolve(result);};
 // The outcome is decided once the updater process has ended, so launcher files are never restored while it runs.
 const settle=code=>{if(finished)return;
  if(!failure&&code===0&&result){if(result.status==='installed')proveLauncher(root,'update',token);return finish(null);}
  const e=failure||Error(error||'Update failed. Your existing game is available; try again.');
  finish(restoreLauncher(root,'update')?Error(sentence(e.message)+RESTORED):e);};
 const abort=e=>{if(failure||finished)return;failure=e;child?.kill();grace=setTimeout(()=>settle(null),10000);};
 // Large downloads report progress; the timeout only fires after timeoutMs without any updater output.
 const expire=()=>abort(Error('Update timed out. Reopen the launcher to retry.'));
 const idle=()=>{clearTimeout(timer);timer=setTimeout(expire,timeoutMs);};idle();const limit=setTimeout(expire,maxMs);
 try{child=spawnImpl(join(root,'runtime','node.exe'),[join(root,'launcher','manual-update.mjs')],{cwd:root,windowsHide:true,shell:false,stdio:['ignore','pipe','pipe']});}catch(e){failure=e;settle(null);return;}
 child.stdout.on('data',chunk=>{if(finished||failure)return;total+=chunk.length;if(total>65536){abort(Error('Unexpected updater response.'));return;}buffer+=chunk.toString();idle();let end;while((end=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,end);buffer=buffer.slice(end+1);try{const value=JSON.parse(line);if(value.type==='progress'&&typeof value.text==='string')onProgress(value.text.slice(0,500));else if(value.type==='error'&&typeof value.text==='string')error=value.text.slice(0,500);else if(value.type==='result'&&['installed','current'].includes(value.status)&&typeof value.version==='string'&&/^[\w.-]{1,80}$/.test(value.version))result={status:value.status,version:value.version};}catch{/* Non-protocol output never counts as a successful update. */}}});
 child.stderr.on('data',chunk=>{error=(error+chunk.toString()).slice(-500);});child.once('error',e=>{failure??=e;settle(null);});child.once('close',settle);
 });}
class ManualUpdater{
 constructor(actions){this.actions=actions;this.busy=false;this.pending=null;}
 start(){if(this.busy||this.actions.blocked())return Promise.reject(Error('Wait for the current operation to finish.'));this.busy=true;
 this.pending=(async()=>{try{if(this.actions.hasSession()&&!await this.actions.confirm())return {status:'cancelled'};if(this.actions.closing())return {status:'cancelled'};await this.actions.stop();this.actions.closeGame();return await this.actions.run();}finally{this.busy=false;}})();return this.pending;
 }
}
module.exports={validLauncherSender,installedVersion,runUpdate,rollbackLauncher,probationToken,proveLauncher,restoreLauncher,hostingProbation,sentence,ManualUpdater};
