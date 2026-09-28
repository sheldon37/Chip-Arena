import {readFile,writeFile,mkdir,rename,rm,lstat} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {gunzipSync} from 'node:zlib';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
export const UPDATE_BASE='https://chip-arena-prototype.sheldon37.chatgpt.site/updates/';
const units=['server.mjs','dist/client','drizzle','release.json'];
const hash=b=>createHash('sha256').update(b).digest('hex');
export function allowedPath(path){return typeof path==='string'&&path.length<200&&!path.includes('\\')&&!path.includes(':')&&!path.split('/').some(s=>!s||s==='.'||s==='..')&&(path==='server.mjs'||path==='release.json'||path.startsWith('dist/client/')||path.startsWith('drizzle/'));}
const exists=async p=>!!(await lstat(p).catch(()=>null));
async function recover(root){const marker=resolve(root,'runtime/update-journal.json');const journal=JSON.parse(await readFile(marker,'utf8').catch(()=>'null'));if(!journal)return;for(const unit of units){const old=resolve(root,'runtime/update-backup',unit),live=resolve(root,unit);if(await exists(old)){await rm(live,{recursive:true,force:true});await mkdir(dirname(live),{recursive:true});await rename(old,live);}else if(journal.absent?.includes(unit))await rm(live,{recursive:true,force:true});}await rm(marker,{force:true});}
export async function applyUpdates(root,{fetchImpl=fetch,force=false,strict=false,log=console.log}={}){
 await recover(root);if(!force&&process.env.CHIP_AUTO_UPDATE==='0')return false;
 try{
  const response=await fetchImpl(UPDATE_BASE+'latest.json',{signal:AbortSignal.timeout(5000),cache:'no-store',redirect:'error'});if(!response.ok)throw Error('Update service unavailable');const manifest=await response.json();
  if(manifest.schema!==1||manifest.nodeMajor!==24||typeof manifest.version!=='string'||!/^[a-f0-9]{64}$/.test(manifest.sha256)||!/^arena-[a-f0-9]{16}\.json\.gz$/.test(manifest.file))throw Error('Unsupported update manifest');
  const current=JSON.parse(await readFile(resolve(root,'release.json'),'utf8').catch(()=>'{}'));if(current.version===manifest.version){if(force)log('Chip Arena is up to date.');return false;}
  if(Number(process.versions.node.split('.')[0])<manifest.nodeMajor)throw Error('This release needs a newer portable runtime');log('Updating Chip Arena to '+manifest.version+'...');
  const r=await fetchImpl(UPDATE_BASE+manifest.file,{signal:AbortSignal.timeout(60000),redirect:'error'});if(!r.ok)throw Error('Update download failed');const bytes=Buffer.from(await r.arrayBuffer());if(bytes.length>32*1024*1024||hash(bytes)!==manifest.sha256)throw Error('Update checksum mismatch');
  const payload=JSON.parse(gunzipSync(bytes,{maxOutputLength:96*1024*1024}).toString());if(payload.schema!==1||!Array.isArray(payload.files)||payload.files.length>1500)throw Error('Invalid update package');
  const stage=resolve(root,'runtime/update-stage'),backup=resolve(root,'runtime/update-backup'),seen=new Set();await rm(stage,{recursive:true,force:true});await mkdir(stage,{recursive:true});
  let total=0;for(const f of payload.files){if(!allowedPath(f.path)||seen.has(f.path)||typeof f.data!=='string')throw Error('Invalid update path');seen.add(f.path);const data=Buffer.from(f.data,'base64');total+=data.length;if(total>64*1024*1024||hash(data)!==f.sha256)throw Error('Invalid update file');const path=resolve(stage,f.path);await mkdir(dirname(path),{recursive:true});await writeFile(path,data);}
  for(const path of ['server.mjs','dist/client/index.html','release.json'])if(!seen.has(path))throw Error('Incomplete update');
  if(JSON.parse(await readFile(resolve(stage,'release.json'),'utf8')).version!==manifest.version)throw Error('Update version mismatch');
  await rm(backup,{recursive:true,force:true});await mkdir(backup,{recursive:true});const absent=[];for(const unit of units){const path=resolve(root,unit),info=await lstat(path).catch(()=>null);if(info?.isSymbolicLink())throw Error('Update cannot replace a linked directory');if(!info)absent.push(unit);}
  await writeFile(resolve(root,'runtime/update-journal.json'),JSON.stringify({absent}));
  try{for(const unit of units){const live=resolve(root,unit),old=resolve(backup,unit),fresh=resolve(stage,unit);await mkdir(dirname(old),{recursive:true});if(await exists(live))await rename(live,old);if(await exists(fresh)){await mkdir(dirname(live),{recursive:true});await rename(fresh,live);}}await rm(resolve(root,'runtime/update-journal.json'));}catch(e){await recover(root);throw e;}
  await rm(stage,{recursive:true,force:true});log('Update installed. Your settings and saved data are preserved.');return true;
 }catch(error){if(strict)throw error;log('Update skipped: '+error.message+'. Starting your existing version.');return false;}
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){let running=false;try{running=(await fetch('http://127.0.0.1:'+Number(process.env.PORT||8788)+'/health',{signal:AbortSignal.timeout(500)})).ok;}catch{}if(running){console.log('Close the running Chip Arena server before updating.');process.exitCode=1;}else await applyUpdates(resolve(dirname(fileURLToPath(import.meta.url)),'..'),{force:true});}
