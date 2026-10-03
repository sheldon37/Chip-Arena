import {readFile,writeFile,mkdir,rename,rm,lstat,open,copyFile} from 'node:fs/promises';
import {createReadStream} from 'node:fs';
import {createHash,randomBytes} from 'node:crypto';
import {gunzipSync,createBrotliDecompress} from 'node:zlib';
import {resolve,dirname,sep} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {spawnSync} from 'node:child_process';
export const UPDATE_BASE='https://chip-arena-prototype.sheldon37.chatgpt.site/updates/';
/** API floor for a freshly activated launcher: it must keep startLauncher() and applyUpdates(root, options). */
export const LAUNCHER_API=2;
const MiB=1024*1024;
/** Schema 1 limits are frozen: every launcher installed before updater v2 enforces exactly these. */
export const LIMITS=Object.freeze({schema1Bytes:32*MiB,schema1Json:96*MiB,schema1Unpacked:64*MiB,schema1Files:1500,schema2Bytes:256*MiB,schema2Unpacked:1024*MiB,schema2Files:4000,schema2Index:4*MiB,manifestBytes:64*1024,launcherFileBytes:2*MiB});
/** The only launcher files a schema-2 payload may replace. Never settings, saves, runtime/ or test-channel.json. */
export const LAUNCHER_FILES=Object.freeze(['launcher/update.mjs','launcher/start.mjs','launcher/manual-update.mjs','launcher/remote-feed.mjs','launcher/local-feed.mjs']);
/** Schema 2 container (inside Brotli): magic, uint32 BE index length, JSON index, then each file's bytes in index order. */
export const PACK_MAGIC='CHIPPACK';
const units=['server.mjs','dist/client','drizzle','release.json'],required=['server.mjs','dist/client/index.html','release.json'];
const STALL_MS=60000,DOWNLOAD_MS=30*60000;
const hash=b=>createHash('sha256').update(b).digest('hex');
// One token per process, shared by the old and the freshly imported module. desktop/update.cjs compares it before and
// after a run: the run that activates a launcher never proves it.
const TOKEN=globalThis.__chipLauncherToken??=randomBytes(8).toString('hex');
export function allowedPath(path){return typeof path==='string'&&path.length<200&&!path.includes('\\')&&!path.includes(':')&&!path.split('/').some(s=>!s||s==='.'||s==='..')&&(path==='server.mjs'||path==='release.json'||path.startsWith('dist/client/')||path.startsWith('drizzle/'));}
export function launcherPath(path){return LAUNCHER_FILES.includes(path);}
/** Windows strips trailing dots and spaces and maps device names (CON, NUL, COM1...) to devices: schema 2 refuses both. */
export function windowsSafe(path){return typeof path==='string'&&path.split('/').every(s=>!/[. ]$/.test(s)&&!/[<>"|?*\x00-\x1f]/.test(s)&&!/^(con|prn|aux|nul|com[1-9\u00b9\u00b2\u00b3]|lpt[1-9\u00b9\u00b2\u00b3])(\.|$)/i.test(s));}
const exists=async p=>!!(await lstat(p).catch(()=>null));
const fileHash=async p=>{try{return hash(await readFile(p));}catch{return null;}};
const inside=(base,path)=>{const p=resolve(base,path);if(!p.startsWith(base+sep))throw Error('Invalid update path');return p;};
async function recover(root){const marker=resolve(root,'runtime/update-journal.json');const journal=JSON.parse(await readFile(marker,'utf8').catch(()=>'null'));if(!journal)return;for(const unit of units){const old=resolve(root,'runtime/update-backup',unit),live=resolve(root,unit);if(await exists(old)){await rm(live,{recursive:true,force:true});await mkdir(dirname(live),{recursive:true});await rename(old,live);}else if(journal.absent?.includes(unit))await rm(live,{recursive:true,force:true});}await rm(marker,{force:true});}
/** Swap the staged game units in under a journal; an error or crash restores the previous units. */
async function installStage(root,stage){
 const backup=resolve(root,'runtime/update-backup');await rm(backup,{recursive:true,force:true});await mkdir(backup,{recursive:true});const absent=[];for(const unit of units){const path=resolve(root,unit),info=await lstat(path).catch(()=>null);if(info?.isSymbolicLink())throw Error('Update cannot replace a linked directory');if(!info)absent.push(unit);}
 await writeFile(resolve(root,'runtime/update-journal.json'),JSON.stringify({absent}));
 try{for(const unit of units){const live=resolve(root,unit),old=resolve(backup,unit),fresh=resolve(stage,unit);await mkdir(dirname(old),{recursive:true});if(await exists(live))await rename(live,old);if(await exists(fresh)){await mkdir(dirname(live),{recursive:true});await rename(fresh,live);}}await rm(resolve(root,'runtime/update-journal.json'));}catch(e){await recover(root);throw e;}
}
async function installSchema1(root,{fetchImpl,force,log}){
 const response=await fetchImpl(UPDATE_BASE+'latest.json',{signal:AbortSignal.timeout(5000),cache:'no-store',redirect:'error'});if(!response.ok)throw Error('Update service unavailable');const manifest=await response.json();
 if(manifest.schema!==1||manifest.nodeMajor!==24||typeof manifest.version!=='string'||!/^[a-f0-9]{64}$/.test(manifest.sha256)||!/^arena-[a-f0-9]{16}\.json\.gz$/.test(manifest.file))throw Error('Unsupported update manifest');
 // A hold notice (the test channel's latest.json once schema 1 no longer fits) is only for launchers older than v2.
 if(manifest.hold===true)throw Error('The test channel is publishing a new update. Try again in a few minutes');
 const current=JSON.parse(await readFile(resolve(root,'release.json'),'utf8').catch(()=>'{}'));if(current.version===manifest.version){if(force)log('Chip Arena is up to date.');return false;}
 if(Number(process.versions.node.split('.')[0])<manifest.nodeMajor)throw Error('This release needs a newer portable runtime');log('Updating Chip Arena to '+manifest.version+'...');
 const r=await fetchImpl(UPDATE_BASE+manifest.file,{signal:AbortSignal.timeout(60000),redirect:'error'});if(!r.ok)throw Error('Update download failed');const bytes=Buffer.from(await r.arrayBuffer());if(bytes.length>LIMITS.schema1Bytes||hash(bytes)!==manifest.sha256)throw Error('Update checksum mismatch');
 const payload=JSON.parse(gunzipSync(bytes,{maxOutputLength:LIMITS.schema1Json}).toString());if(payload.schema!==1||!Array.isArray(payload.files)||payload.files.length>LIMITS.schema1Files)throw Error('Invalid update package');
 const stage=resolve(root,'runtime/update-stage'),seen=new Set();await rm(stage,{recursive:true,force:true});await mkdir(stage,{recursive:true});
 let total=0;for(const f of payload.files){if(!allowedPath(f.path)||seen.has(f.path)||typeof f.data!=='string')throw Error('Invalid update path');seen.add(f.path);const data=Buffer.from(f.data,'base64');total+=data.length;if(total>LIMITS.schema1Unpacked||hash(data)!==f.sha256)throw Error('Invalid update file');const path=resolve(stage,f.path);await mkdir(dirname(path),{recursive:true});await writeFile(path,data);}
 for(const path of required)if(!seen.has(path))throw Error('Incomplete update');
 if(JSON.parse(await readFile(resolve(stage,'release.json'),'utf8')).version!==manifest.version)throw Error('Update version mismatch');
 await installStage(root,stage);await rm(stage,{recursive:true,force:true});
 // Schema 1 carries no launcher, so a launcher staged with an earlier schema-2 game must not activate over this one.
 await rm(runtimeFile(root,'launcher-pending'),{recursive:true,force:true});
 log('Update installed. Your settings and saved data are preserved.');return true;
}
const hex64=v=>typeof v==='string'&&/^[a-f0-9]{64}$/.test(v),count=(v,max,min=0)=>Number.isSafeInteger(v)&&v>=min&&v<=max;
function launcherList(list){if(!Array.isArray(list)||list.length>LAUNCHER_FILES.length)return null;const seen=new Set();for(const f of list){if(!f||!launcherPath(f.path)||seen.has(f.path)||!hex64(f.sha256))return null;seen.add(f.path);}return list.map(f=>({path:f.path,sha256:f.sha256}));}
/** Validate a schema-2 feed manifest (latest-v2.json). maxBytes can only lower the launcher's own cap. */
export function checkManifest2(m,maxBytes=LIMITS.schema2Bytes){
 const cap=Math.min(maxBytes,LIMITS.schema2Bytes),launcher=launcherList(m?.launcher);
 if(!m||m.schema!==2||m.encoding!=='br'||!count(m.nodeMajor,99,20)||typeof m.version!=='string'||!/^[\w.-]{1,80}$/.test(m.version)||typeof m.build!=='string'||!/^[a-f0-9]{24}$/.test(m.build)||!hex64(m.sha256)||typeof m.file!=='string'||!/^arena-[a-f0-9]{16}\.pack$/.test(m.file)||m.file.slice(6,22)!==m.sha256.slice(0,16)||!count(m.unpackedBytes,LIMITS.schema2Unpacked)||!count(m.files,LIMITS.schema2Files,1)||!launcher)throw Error('Unsupported update manifest');
 if(!count(m.bytes,cap,1))throw Error('Update is larger than this launcher accepts ('+Math.floor(cap/MiB)+' MiB)');
 return {...m,launcher};
}
async function readJson(response,limit,error){if(Number(response.headers?.get?.('content-length'))>limit)throw Error(error);const parts=[];let size=0;if(response.body)for await(const chunk of response.body){size+=chunk.length;if(size>limit)throw Error(error);parts.push(chunk);}try{return JSON.parse(Buffer.concat(parts).toString());}catch{throw Error(error);}}
const readRecord=async path=>JSON.parse(await readFile(path,'utf8').catch(()=>'null'));
const runtimeFile=(root,name)=>resolve(root,'runtime',name);
/** True when every listed launcher file is installed, or already staged for the next start, with the expected hash. */
/** A staged launcher the feed no longer lists (for example after the channel reverted a version) must not activate. */
async function dropStalePending(root,list){const plan=await readRecord(runtimeFile(root,'launcher-pending/pending.json'));if(!plan)return;const want=new Map(list.map(f=>[f.path,f.sha256]));if(!Array.isArray(plan.files)||plan.files.some(f=>want.get(f?.path)!==f?.sha256))await rm(runtimeFile(root,'launcher-pending'),{recursive:true,force:true});}
async function launcherCurrent(root,list){const plan=await readRecord(runtimeFile(root,'launcher-pending/pending.json'));const staged=new Map((Array.isArray(plan?.files)?plan.files:[]).map(f=>[f?.path,f?.sha256]));for(const f of list)if(await fileHash(resolve(root,f.path))!==f.sha256&&staged.get(f.path)!==f.sha256)return false;return true;}
const mb=n=>(n/1e6).toFixed(1);
/** Stream the payload to disk with a byte cap, stall timeout and incremental SHA-256; nothing is parsed before it verifies. */
async function download(fetchImpl,url,part,m,log){
 const controller=new AbortController(),hard=setTimeout(()=>controller.abort(),DOWNLOAD_MS);let timer;const alive=()=>{clearTimeout(timer);timer=setTimeout(()=>controller.abort(),STALL_MS);};alive();
 const file=await open(part,'w'),h=createHash('sha256');let size=0,last=Date.now(),shown=0;
 try{
  const r=await fetchImpl(url,{signal:controller.signal,redirect:'error',cache:'no-store'});if(!r.ok||!r.body)throw Error('Update download failed');
  if(Number(r.headers?.get?.('content-length'))>m.bytes)throw Error('Update is larger than its manifest');
  log('Downloading update: '+mb(m.bytes)+' MB');
  for await(const chunk of r.body){size+=chunk.length;if(size>m.bytes)throw Error('Update is larger than its manifest');h.update(chunk);await file.write(chunk);alive();const pct=Math.floor(size*100/m.bytes),now=Date.now();if(now-last>=30000||pct>shown&&now-last>=2000){last=now;shown=pct;log('Downloading update: '+mb(size)+' of '+mb(m.bytes)+' MB ('+pct+'%)');}}
 }catch(e){if(controller.signal.aborted)throw Error('Update download timed out');throw /^Update /.test(e?.message)?e:Error('Update download failed');}
 finally{clearTimeout(timer);clearTimeout(hard);await file.close();}
 if(size!==m.bytes)throw Error('Update download was incomplete');if(h.digest('hex')!==m.sha256)throw Error('Update checksum mismatch');
}
function checkIndex(bytes,m){
 let index;try{index=JSON.parse(bytes.toString());}catch{throw Error('Invalid update package');}
 if(index?.schema!==2||index.version!==m.version||!Array.isArray(index.files)||index.files.length!==m.files)throw Error('Invalid update package');
 const seen=new Set(),launcher=[];let total=0;
 for(const f of index.files){const isLauncher=launcherPath(f?.path);if(!f||!(allowedPath(f.path)||isLauncher)||!windowsSafe(f.path)||seen.has(f.path.toLowerCase())||!count(f.size,LIMITS.schema2Unpacked)||!hex64(f.sha256)||isLauncher&&f.size>LIMITS.launcherFileBytes)throw Error('Invalid update path');seen.add(f.path.toLowerCase());total+=f.size;if(isLauncher)launcher.push(f);}
 if(total!==m.unpackedBytes)throw Error('Invalid update package');
 // The feed manifest's launcher list drives the up-to-date check, so it must describe this payload exactly.
 if(launcher.length!==m.launcher.length||launcher.some(f=>!m.launcher.some(e=>e.path===f.path&&e.sha256===f.sha256)))throw Error('Invalid update package');
 return index.files;
}
/** Decompress and unpack a verified schema-2 file without holding it in memory; every file is size- and hash-checked. */
async function unpack(part,m,stage,next,log){
 const source=createReadStream(part),input=source.pipe(createBrotliDecompress()),limit=12+LIMITS.schema2Index+m.unpackedBytes,made=new Set(),game=new Set(),launcher=[];
 source.on('error',e=>input.destroy(e));
 let phase='header',need=12,parts=[],have=0,files,i=0,entry,out,h,left=0,total=0,shown=Date.now();
 const end=async()=>{await out.close();out=null;if(h.digest('hex')!==entry.sha256)throw Error('Invalid update file');if(launcherPath(entry.path))launcher.push(entry);else game.add(entry.path);};
 const begin=async()=>{for(;;){if(i===files.length){phase='done';return;}entry=files[i++];const path=inside(launcherPath(entry.path)?next:stage,entry.path),dir=dirname(path);if(!made.has(dir)){await mkdir(dir,{recursive:true});made.add(dir);}out=await open(path,'wx');h=createHash('sha256');left=entry.size;phase='data';if(left>0)return;await end();}};
 try{
  for await(let chunk of input){
   total+=chunk.length;if(total>limit)throw Error('Invalid update package');
   // The desktop updater stops after 90 s without output; slow disks and antivirus scans can make unpacking long.
   if(Date.now()-shown>=15000){shown=Date.now();log('Unpacking update: '+mb(total)+' of '+mb(m.unpackedBytes)+' MB');}
   while(chunk.length){
    if(phase==='done')throw Error('Invalid update package');
    if(phase==='data'){const n=Math.min(left,chunk.length),piece=chunk.subarray(0,n);h.update(piece);await out.write(piece);left-=n;chunk=chunk.subarray(n);if(!left){await end();await begin();}continue;}
    const n=Math.min(need-have,chunk.length);parts.push(chunk.subarray(0,n));have+=n;chunk=chunk.subarray(n);if(have<need)continue;
    const bytes=Buffer.concat(parts);parts=[];have=0;
    if(phase==='header'){if(bytes.subarray(0,8).toString('latin1')!==PACK_MAGIC)throw Error('Invalid update package');need=bytes.readUInt32BE(8);if(need<2||need>LIMITS.schema2Index)throw Error('Invalid update package');phase='index';}
    else{files=checkIndex(bytes,m);await begin();}
   }
  }
 }catch(e){throw /^(Invalid|Update) /.test(e?.message)?e:Error('Invalid update package');}
 finally{await out?.close();input.destroy();source.destroy();}
 if(phase!=='done')throw Error('Invalid update package');
 return {game,launcher};
}
const syntaxOk=(path,log)=>{log('Checking '+path.split(sep).slice(-2).join('/')+'...');return spawnSync(process.execPath,['--check',path],{stdio:'ignore',timeout:20000,windowsHide:true}).status===0;};
async function installSchema2(root,m,{fetchImpl,force,log}){
 const current=JSON.parse(await readFile(resolve(root,'release.json'),'utf8').catch(()=>'{}'));if(current.version===m.version&&await launcherCurrent(root,m.launcher)){await dropStalePending(root,m.launcher);if(force)log('Chip Arena is up to date.');return false;}
 if(Number(process.versions.node.split('.')[0])<m.nodeMajor)throw Error('This release needs a newer portable runtime');log('Updating Chip Arena to '+m.version+'...');
 const runtime=resolve(root,'runtime'),part=resolve(runtime,'update-download.part'),stage=resolve(runtime,'update-stage'),next=resolve(runtime,'launcher-next'),pending=resolve(runtime,'launcher-pending');
 await mkdir(runtime,{recursive:true});
 try{
  await download(fetchImpl,UPDATE_BASE+m.file,part,m,log);
  for(const dir of [stage,next]){await rm(dir,{recursive:true,force:true});await mkdir(dir,{recursive:true});}
  log('Unpacking and checking '+m.files+' files...');const {game,launcher}=await unpack(part,m,stage,next,log);await rm(part,{force:true});
  for(const path of required)if(!game.has(path))throw Error('Incomplete update');
  if(JSON.parse(await readFile(resolve(stage,'release.json'),'utf8')).version!==m.version)throw Error('Update version mismatch');
  const changed=[];for(const f of launcher)if(await fileHash(resolve(root,f.path))!==f.sha256)changed.push(f);
  for(const f of changed)if(f.path.endsWith('.mjs')&&!syntaxOk(resolve(next,f.path),log))throw Error('Update contains an invalid launcher file');
  log('Installing update files...');await installStage(root,stage);
  // A launcher staged by an older update must never activate over this one.
  await rm(pending,{recursive:true,force:true});
  if(changed.length){await writeFile(resolve(next,'pending.json'),JSON.stringify({version:m.version,files:changed.map(({path,sha256,size})=>({path,sha256,size}))}));await rename(next,pending);log('Launcher update staged; it is applied the next time Chip Arena starts or updates.');}
  log('Update installed. Your settings and saved data are preserved.');return true;
 }finally{await rm(part,{force:true});await rm(stage,{recursive:true,force:true});await rm(next,{recursive:true,force:true});}
}
export async function applyUpdates(root,{fetchImpl=fetch,force=false,strict=false,log=console.log,schema2=false,maxBytes=LIMITS.schema2Bytes}={}){
 await recover(root);await recoverLauncher(root);if(!force&&process.env.CHIP_AUTO_UPDATE==='0')return false;
 try{
  let manifest=null;
  // Schema 2 is opt-in per feed. Only a missing latest-v2.json (404) falls back to schema 1; any other failure stops.
  if(schema2){const r=await fetchImpl(UPDATE_BASE+'latest-v2.json',{signal:AbortSignal.timeout(5000),cache:'no-store',redirect:'error'});if(r.ok)manifest=checkManifest2(await readJson(r,LIMITS.manifestBytes,'Unsupported update manifest'),maxBytes);else if(r.status!==404)throw Error('Update service unavailable');}
  // A new launcher is proven (and its backup dropped) by desktop/update.cjs, which is never self-updated.
  return manifest?await installSchema2(root,manifest,{fetchImpl,force,log}):await installSchema1(root,{fetchImpl,force,log});
 }catch(error){if(strict)throw error;log('Update skipped: '+error.message+'. Starting your existing version.');return false;}
}
/** Restore the launcher files saved before the last activation (journal first, then probation). */
export async function rollbackLauncher(root){
 let restored=false;
 for(const name of ['launcher-journal.json','launcher-probation.json']){const record=await readRecord(runtimeFile(root,name));if(!record)continue;for(const path of Array.isArray(record.files)?record.files:[]){if(!launcherPath(path))continue;const saved=resolve(root,'runtime/launcher-backup',path),live=resolve(root,path);if(await exists(saved))await copyFile(saved,live);else if(record.absent?.includes(path))await rm(live,{force:true});}await rm(runtimeFile(root,name),{force:true});restored=true;}
 await rm(runtimeFile(root,'launcher-pending'),{recursive:true,force:true});await rm(runtimeFile(root,'launcher-backup'),{recursive:true,force:true});return restored;
}
/** A crash during activation leaves the journal: restore the previous launcher before anything else runs. */
export async function recoverLauncher(root){return await exists(runtimeFile(root,'launcher-journal.json'))?rollbackLauncher(root):false;}
/**
 * Verify a staged launcher update, then swap it in file by file under a journal, keeping a rollback copy. A launcher
 * still on probation keeps its original backup, so a rollback always returns to the last proven launcher.
 */
export async function activateLauncher(root,{log=console.log}={}){
 const pending=runtimeFile(root,'launcher-pending'),plan=await readRecord(resolve(pending,'pending.json'));if(!plan)return false;
 log('Applying the launcher update...');
 try{
  if(!Array.isArray(plan.files)||!plan.files.length||plan.files.length>LAUNCHER_FILES.length||new Set(plan.files.map(f=>f?.path)).size!==plan.files.length)throw Error('invalid');
  for(const f of plan.files){if(!launcherPath(f?.path)||!hex64(f.sha256))throw Error('invalid');const staged=resolve(pending,f.path),data=await readFile(staged);if(data.length>LIMITS.launcherFileBytes||hash(data)!==f.sha256||f.path.endsWith('.mjs')&&!syntaxOk(staged,log))throw Error('invalid');if((await lstat(resolve(root,f.path)).catch(()=>null))?.isSymbolicLink())throw Error('invalid');}
 }catch{await rm(pending,{recursive:true,force:true});log('Discarded an invalid staged launcher update.');return false;}
 const backup=runtimeFile(root,'launcher-backup'),journal=runtimeFile(root,'launcher-journal.json'),prior=await readRecord(runtimeFile(root,'launcher-probation.json'));
 const kept=Array.isArray(prior?.files)?prior.files.filter(launcherPath):null,absent=kept&&Array.isArray(prior.absent)?prior.absent.filter(p=>kept.includes(p)):[];
 if(!kept)await rm(backup,{recursive:true,force:true});
 for(const f of plan.files){if(kept?.includes(f.path))continue;const live=resolve(root,f.path);if(await exists(live)){await mkdir(dirname(resolve(backup,f.path)),{recursive:true});await copyFile(live,resolve(backup,f.path));}else absent.push(f.path);}
 const record={token:TOKEN,version:String(plan.version).slice(0,80),files:[...new Set([...(kept||[]),...plan.files.map(f=>f.path)])],absent};await writeFile(journal,JSON.stringify(record));
 try{for(const f of plan.files){await mkdir(dirname(resolve(root,f.path)),{recursive:true});await rename(resolve(pending,f.path),resolve(root,f.path));}await writeFile(runtimeFile(root,'launcher-probation.json'),JSON.stringify(record));await rm(journal,{force:true});}
 catch(e){await rollbackLauncher(root);throw e;}
 await rm(pending,{recursive:true,force:true});return true;
}
/**
 * Entry points call this before updating: it applies a staged launcher update, then loads the new update.mjs.
 * If activation or loading fails, the previous launcher files are restored and the running module is used.
 */
export async function startLauncher(root,{log=console.log}={}){
 const self=await import(import.meta.url);
 try{await recoverLauncher(root);if(!await activateLauncher(root,{log}))return self;}catch(e){log('Launcher update skipped: '+e.message+'.');return self;}
 try{const fresh=await import(pathToFileURL(resolve(root,'launcher/update.mjs')).href+'?activated='+TOKEN);if(!(fresh.LAUNCHER_API>=2)||typeof fresh.applyUpdates!=='function'||typeof fresh.startLauncher!=='function')throw Error('incompatible launcher');log('Launcher updated.');return fresh;}
 catch(e){await rollbackLauncher(root);log('The new launcher could not start ('+e.message+'); the previous launcher was restored.');return self;}
}
if(process.argv[1]&&!new URL(import.meta.url).search&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){let running=false;try{running=(await fetch('http://127.0.0.1:'+Number(process.env.PORT||8788)+'/health',{signal:AbortSignal.timeout(500)})).ok;}catch{}if(running){console.log('Close the running Chip Arena server before updating.');process.exitCode=1;}else{const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');await (await startLauncher(root)).applyUpdates(root,{force:true});}}
