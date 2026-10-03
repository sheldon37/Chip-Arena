// Applies the candidate's real feed files with the candidate's real launcher/update.mjs in throwaway installs.
// Schema 1 (latest.json, every launcher) and, when present, schema 2 (latest-v2.json, updater v2) are both checked.
import assert from 'node:assert/strict';
import {readFile,mkdtemp,mkdir,writeFile,rm} from 'node:fs/promises';
import {existsSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {gunzipSync,brotliDecompressSync} from 'node:zlib';
import {pathToFileURL} from 'node:url';
import {localFeed} from './local-feed.mjs';
import {feedProblems} from './source-privacy.mjs';
const updater=await import(pathToFileURL(resolve(process.argv[3],'launcher/update.mjs')).href);
const {applyUpdates,UPDATE_BASE,allowedPath}=updater;
const feed=resolve(process.argv[2]),hash=b=>createHash('sha256').update(b).digest('hex'),quiet=()=>{};
const has=name=>existsSync(join(feed,name)),checks=[],summary={result:'passed'};
assert(has('latest.json')||has('latest-v2.json'),'The feed has no manifest');
// The channel is public: no payload may carry the source archive, a src/ tree or source maps.
const leaked=feedProblems(feed);assert.deepEqual(leaked,[],'feed carries source: '+leaked.join(', '));checks.push('no source archive, src/ tree or source map in any payload');
async function install(){
 const root=await mkdtemp(join(tmpdir(),'chip-candidate-'));
 await mkdir(join(root,'dist/client'),{recursive:true});await mkdir(join(root,'drizzle'));await mkdir(join(root,'runtime'));
 await writeFile(join(root,'server.mjs'),'old');await writeFile(join(root,'dist/client/index.html'),'old');
 await writeFile(join(root,'release.json'),JSON.stringify({version:'test-seed'}));await writeFile(join(root,'settings-sentinel'),'untouched');
 return root;
}
let v1=null;
if(has('latest.json')){
 const manifest=JSON.parse(await readFile(join(feed,'latest.json')));
 assert.match(manifest.file,/^arena-[a-f0-9]{16}\.json\.gz$/);
 const bytes=await readFile(join(feed,manifest.file));
 assert.equal(hash(bytes),manifest.sha256);assert.equal(bytes.length,manifest.bytes);
 // 32 MiB is the hard cap of every launcher installed before updater v2.
 assert(bytes.length<=32*1024*1024,'schema 1 exceeds what installed launchers accept');
 const payload=JSON.parse(gunzipSync(bytes));
 assert(payload.files.every(f=>allowedPath(f.path)));
 const root=await install();
 try{
  const fetchImpl=localFeed(feed,UPDATE_BASE);
  await assert.rejects(fetchImpl('https://unexpected.example/latest.json'));
  assert.equal(await applyUpdates(root,{fetchImpl,force:true,strict:true,log:quiet}),true);
  for(const f of payload.files){assert.equal(hash(await readFile(join(root,f.path))),f.sha256,f.path);}
  assert.equal(await readFile(join(root,'settings-sentinel'),'utf8'),'untouched');
  assert.equal(await applyUpdates(root,{fetchImpl,force:true,strict:true,log:quiet}),false);
  await assert.rejects(applyUpdates(root,{force:true,strict:true,log:quiet,fetchImpl:async()=>{throw Error('offline');}}),/offline/);
  const corrupt=async url=>String(url).endsWith('latest.json')?Response.json({...manifest,version:'bad',sha256:'0'.repeat(64)}):new Response(bytes);
  await assert.rejects(applyUpdates(root,{fetchImpl:corrupt,force:true,strict:true,log:quiet}),/checksum/);
  assert.equal(JSON.parse(await readFile(join(root,'release.json'))).version,manifest.version);
 }finally{await rm(root,{recursive:true,force:true});}
 v1={manifest,files:new Map(payload.files.map(f=>[f.path,f.sha256]))};summary.version=manifest.version;summary.filesVerified=payload.files.length;summary.schema1Bytes=bytes.length;
 checks.push('schema 1: apply actual payload','verify every installed file','same-version skip','offline refusal','corrupt hash refusal','settings sentinel','test feed URL restriction','32 MiB installed-launcher cap');
}
if(has('latest-v2.json')){
 assert.equal(typeof updater.checkManifest2,'function','latest-v2.json needs a launcher with updater v2');
 const manifest=updater.checkManifest2(JSON.parse(await readFile(join(feed,'latest-v2.json'))));
 const pack=await readFile(join(feed,manifest.file));assert.equal(hash(pack),manifest.sha256);assert.equal(pack.length,manifest.bytes);
 // Decode independently of the launcher: magic, index, every file hash, no trailing bytes.
 const body=brotliDecompressSync(pack);assert.equal(body.subarray(0,8).toString('latin1'),updater.PACK_MAGIC);
 const length=body.readUInt32BE(8),index=JSON.parse(body.subarray(12,12+length));let at=12+length;const entries=new Map();
 assert.equal(index.version,manifest.version);assert.equal(index.files.length,manifest.files);
 for(const f of index.files){assert.equal(hash(body.subarray(at,at+f.size)),f.sha256,f.path);at+=f.size;entries.set(f.path,f.sha256);}
 assert.equal(at,body.length,'schema 2 has trailing bytes');
 const launcher=[...entries].filter(([path])=>updater.launcherPath(path));
 assert.deepEqual(launcher.map(([path,sha256])=>({path,sha256})).sort((a,b)=>a.path<b.path?-1:1),[...manifest.launcher].sort((a,b)=>a.path<b.path?-1:1));
 if(v1){
  // Both feeds of one build carry the same game: version, build identity and every game file.
  assert.equal(manifest.version,v1.manifest.version);assert.equal(manifest.build,v1.manifest.build);
  assert.deepEqual(new Map([...entries].filter(([path])=>!updater.launcherPath(path))),v1.files);
 }
 const root=await install();
 try{
  const fetchImpl=localFeed(feed,UPDATE_BASE),logs=[];
  assert.equal(await applyUpdates(root,{fetchImpl,force:true,strict:true,schema2:true,log:m=>logs.push(m)}),true);
  for(const [path,sha256] of entries)if(!updater.launcherPath(path))assert.equal(hash(await readFile(join(root,path))),sha256,path);
  for(const [path,sha256] of launcher)assert.equal(hash(await readFile(join(root,'runtime/launcher-pending',path))),sha256,'staged '+path);
  assert.equal(await readFile(join(root,'settings-sentinel'),'utf8'),'untouched');
  assert.equal(await applyUpdates(root,{fetchImpl,force:true,strict:true,schema2:true,log:quiet}),false);
  // Next start: the payload's own launcher files are verified, swapped in, and its update.mjs must load.
  const started=[],fresh=await updater.startLauncher(root,{log:m=>started.push(m)});
  assert(started.includes('Launcher updated.'),started.join(' | '));
  for(const [path,sha256] of launcher)assert.equal(hash(await readFile(join(root,path))),sha256,'activated '+path);
  assert.equal(await fresh.applyUpdates(root,{fetchImpl,force:true,strict:true,schema2:true,log:quiet}),false);
  const flipped=Buffer.from(pack);flipped[flipped.length>>1]^=0xff;
  const corrupt=async url=>String(url).endsWith('.json')?Response.json({...manifest,version:'0.2.corrupt'}):new Response(flipped);
  await assert.rejects(applyUpdates(root,{fetchImpl:corrupt,force:true,strict:true,schema2:true,log:quiet}),/checksum/);
  assert.equal(JSON.parse(await readFile(join(root,'release.json'))).version,manifest.version);
 }finally{await rm(root,{recursive:true,force:true});}
 Object.assign(summary,{version:manifest.version,schema2Bytes:pack.length,schema2Files:entries.size,launcher:launcher.map(([path])=>path)});
 checks.push('schema 2: apply actual payload','verify every installed file','launcher files staged then activated on next start','new update.mjs loads','same-version skip','corrupt hash refusal','settings sentinel',...(v1?['schema 1 and schema 2 carry identical game files']:[]));
}
console.log(JSON.stringify({...summary,checks}));
