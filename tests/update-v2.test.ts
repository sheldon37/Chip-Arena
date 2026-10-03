// Updater v2: schema-2 container, launcher self-update (stage → next-start activation → rollback) and packager profiles.
import {test} from 'node:test';import assert from 'node:assert/strict';
import {mkdtemp,mkdir,writeFile,readFile,rm,copyFile,readdir} from 'node:fs/promises';import {existsSync} from 'node:fs';
import {tmpdir} from 'node:os';import {join} from 'node:path';import {fileURLToPath} from 'node:url';
import {createHash,randomBytes} from 'node:crypto';import {brotliCompressSync,gunzipSync} from 'node:zlib';
import {createRequire} from 'node:module';import {EventEmitter} from 'node:events';import {PassThrough} from 'node:stream';
import * as updater from '../launcher/update.mjs';
import {encodeSchema1,encodeSchema2,manifest2,packagerConfig,launcherEntries,schema1Problem,MiB} from '../scripts/update-format.mjs';
const {runUpdate}=createRequire(import.meta.url)('../desktop/update.cjs');
const {applyUpdates,startLauncher,activateLauncher,confirmLauncher,recoverLauncher,checkManifest2,LIMITS,UPDATE_BASE}=updater;
const hash=(b:Buffer|string)=>createHash('sha256').update(b).digest('hex');
const repoFile=(path:string)=>fileURLToPath(new URL('../'+path,import.meta.url));
const SOURCE=await readFile(repoFile('launcher/update.mjs'),'utf8');
const quiet=()=>{};
type Entry={path:string;data:Buffer};
async function install(){
 const root=await mkdtemp(join(tmpdir(),'chip-v2-'));for(const dir of ['dist/client','drizzle','launcher','runtime'])await mkdir(join(root,dir),{recursive:true});
 await writeFile(join(root,'server.mjs'),'old-server');await writeFile(join(root,'dist/client/index.html'),'old-client');await writeFile(join(root,'release.json'),JSON.stringify({version:'old'}));
 for(const name of ['update.mjs','start.mjs','manual-update.mjs'])await copyFile(repoFile('launcher/'+name),join(root,'launcher',name));
 // User data and per-install configuration that no update may touch.
 await writeFile(join(root,'settings.json'),'saved');await writeFile(join(root,'runtime/node.exe'),'runtime');await writeFile(join(root,'test-channel.json'),'{"base":"https://channel.test/"}');
 return root;
}
const game=(version:string,extra:Entry[]=[]):Entry[]=>[{path:'server.mjs',data:Buffer.from('new-server '+version)},{path:'dist/client/index.html',data:Buffer.from('new-client')},{path:'drizzle/0000.sql',data:Buffer.from('SELECT 1;')},...extra,{path:'release.json',data:Buffer.from(JSON.stringify({version}))}];
function feed(version:string,{extra=[],launcher=[]}:{extra?:Entry[];launcher?:Entry[]}={}){
 const all=[...game(version,extra),...launcher],{pack,unpacked}=encodeSchema2(all,version,1);
 // nodeMajor 20 keeps these tests independent of the Node 24 portable runtime.
 const manifest:Record<string,any>={...manifest2({build:'b'.repeat(24),version,pack,unpacked,count:all.length,launcher}),nodeMajor:20};
 return {manifest,pack,files:{'latest-v2.json':manifest,[manifest.file]:pack} as Record<string,unknown>};
}
function serve(files:Record<string,unknown>,calls:string[]=[]){return async(url:string)=>{const name=url.slice(UPDATE_BASE.length);calls.push(name);const v=files[name];if(v===undefined)return new Response('missing',{status:404});if(v instanceof Response)return v;return Buffer.isBuffer(v)?new Response(new Uint8Array(v)):Response.json(v);};}
function container(index:object,bodies:Buffer[],trailer=Buffer.alloc(0)){const i=Buffer.from(JSON.stringify(index)),h=Buffer.alloc(12);h.write('CHIPPACK',0,'latin1');h.writeUInt32BE(i.length,8);return brotliCompressSync(Buffer.concat([h,i,...bodies,trailer]));}
function manifestFor(pack:Buffer,version:string,files:number,unpackedBytes:number,launcher:{path:string;sha256:string}[]=[]){const sha256=hash(pack);return {schema:2,encoding:'br',nodeMajor:20,build:'b'.repeat(24),version,file:'arena-'+sha256.slice(0,16)+'.pack',sha256,bytes:pack.length,unpackedBytes,files,launcher};}
const run=(root:string,fetchImpl:any,log:(m:string)=>void=quiet)=>applyUpdates(root,{fetchImpl,force:true,strict:true,schema2:true,log});
async function untouched(root:string){assert.equal(await readFile(join(root,'server.mjs'),'utf8'),'old-server');assert.equal(JSON.parse(await readFile(join(root,'release.json'),'utf8')).version,'old');assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved');assert.equal(existsSync(join(root,'runtime/update-download.part')),false);assert.equal(existsSync(join(root,'runtime/launcher-pending')),false);}
const marker=(source:string,tag:string)=>Buffer.from(source+'\n// '+tag+'\n');

test('schema 2 installs the game, stages launcher files for the next start, keeps settings and skips the same version',async()=>{
 const root=await install();try{
  const manual=marker(await readFile(repoFile('launcher/manual-update.mjs'),'utf8'),'manual v2'),next=marker(SOURCE,'update v2');
  const {files}=feed('0.2.v2test',{launcher:[{path:'launcher/manual-update.mjs',data:manual},{path:'launcher/update.mjs',data:next}]}),logs:string[]=[];
  assert.equal(await run(root,serve(files),m=>logs.push(m)),true);
  assert.equal(await readFile(join(root,'server.mjs'),'utf8'),'new-server 0.2.v2test');assert.equal(await readFile(join(root,'drizzle/0000.sql'),'utf8'),'SELECT 1;');
  assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved');assert.equal(await readFile(join(root,'runtime/node.exe'),'utf8'),'runtime');assert.equal(await readFile(join(root,'test-channel.json'),'utf8'),'{"base":"https://channel.test/"}');
  // Launcher files are only staged by this run; the running launcher is unchanged until the next start.
  assert.equal(await readFile(join(root,'launcher/update.mjs'),'utf8'),SOURCE);assert.ok(logs.some(m=>/Launcher update staged/.test(m)));
  const plan=JSON.parse(await readFile(join(root,'runtime/launcher-pending/pending.json'),'utf8'));assert.deepEqual(plan.files.map((f:any)=>f.path).sort(),['launcher/manual-update.mjs','launcher/update.mjs']);
  assert.equal(await run(root,serve(files)),false,'same version with the launcher already staged is current');
  // Next start: activation swaps the files, keeps a rollback copy and loads the new update.mjs.
  const started=[] as string[],fresh=await startLauncher(root,{log:m=>started.push(m)});
  assert.notEqual(fresh,updater);assert.equal(typeof fresh.applyUpdates,'function');assert.ok(started.includes('Launcher updated.'));
  assert.equal(await readFile(join(root,'launcher/update.mjs'),'utf8'),next.toString());assert.equal(await readFile(join(root,'launcher/manual-update.mjs'),'utf8'),manual.toString());
  assert.equal(await readFile(join(root,'runtime/launcher-backup/launcher/update.mjs'),'utf8'),SOURCE);assert.ok(existsSync(join(root,'runtime/launcher-probation.json')));assert.equal(existsSync(join(root,'runtime/launcher-pending')),false);
  assert.equal(await fresh.applyUpdates(root,{fetchImpl:serve(files),force:true,strict:true,schema2:true,log:quiet}),false);
  assert.ok(existsSync(join(root,'runtime/launcher-probation.json')),'the activating run never confirms its own launcher');
  // A later process (different token) that completes an update check confirms the launcher and drops the backup.
  const probation=JSON.parse(await readFile(join(root,'runtime/launcher-probation.json'),'utf8'));await writeFile(join(root,'runtime/launcher-probation.json'),JSON.stringify({...probation,token:'later-run'}));
  assert.equal(await run(root,serve(files)),false);assert.equal(existsSync(join(root,'runtime/launcher-probation.json')),false);assert.equal(existsSync(join(root,'runtime/launcher-backup')),false);
  assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved');
 }finally{await rm(root,{recursive:true,force:true});}
});

test('a new launcher that cannot load is rolled back and the running launcher keeps working',async()=>{
 const root=await install();try{
  const {files}=feed('0.2.badapi',{launcher:[{path:'launcher/update.mjs',data:Buffer.from('export const LAUNCHER_API=1;\n')}]});
  assert.equal(await run(root,serve(files)),true);assert.ok(existsSync(join(root,'runtime/launcher-pending/pending.json')));
  const logs:string[]=[],used=await startLauncher(root,{log:m=>logs.push(m)});
  assert.equal(used,updater);assert.ok(logs.some(m=>/previous launcher was restored/.test(m)));
  assert.equal(await readFile(join(root,'launcher/update.mjs'),'utf8'),SOURCE);for(const left of ['launcher-probation.json','launcher-backup','launcher-pending','launcher-journal.json'])assert.equal(existsSync(join(root,'runtime',left)),false,left);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('launcher files are verified twice: syntax errors are refused before install and tampered staging is discarded',async()=>{
 const root=await install();try{
  const broken=feed('0.2.syntax',{launcher:[{path:'launcher/start.mjs',data:Buffer.from('export const = ;')}]});
  await assert.rejects(run(root,serve(broken.files)),/invalid launcher file/);await untouched(root);
  const good=feed('0.2.tamper',{launcher:[{path:'launcher/start.mjs',data:marker(await readFile(repoFile('launcher/start.mjs'),'utf8'),'v2')}]});
  assert.equal(await run(root,serve(good.files)),true);await writeFile(join(root,'runtime/launcher-pending/launcher/start.mjs'),'tampered');
  const logs:string[]=[];assert.equal(await activateLauncher(root,{log:m=>logs.push(m)}),false);assert.deepEqual(logs,['Discarded an invalid staged launcher update.']);
  assert.equal(await readFile(join(root,'launcher/start.mjs'),'utf8'),await readFile(repoFile('launcher/start.mjs'),'utf8'));assert.equal(existsSync(join(root,'runtime/launcher-pending')),false);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('a crash during launcher activation is rolled back before the next update check',async()=>{
 const root=await install();try{
  await mkdir(join(root,'runtime/launcher-backup/launcher'),{recursive:true});await writeFile(join(root,'runtime/launcher-backup/launcher/start.mjs'),'original-start');
  await writeFile(join(root,'launcher/start.mjs'),'half-activated');await writeFile(join(root,'launcher/remote-feed.mjs'),'new-file');
  await writeFile(join(root,'runtime/launcher-journal.json'),JSON.stringify({token:'crashed',files:['launcher/start.mjs','launcher/remote-feed.mjs','../settings.json'],absent:['launcher/remote-feed.mjs']}));
  await applyUpdates(root,{fetchImpl:async()=>{throw Error('offline');},log:quiet});
  assert.equal(await readFile(join(root,'launcher/start.mjs'),'utf8'),'original-start');assert.equal(existsSync(join(root,'launcher/remote-feed.mjs')),false);
  assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved','non-launcher paths in a journal are ignored');assert.equal(existsSync(join(root,'runtime/launcher-journal.json')),false);
  assert.equal(await recoverLauncher(root),false);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('the desktop updater restores the previous launcher when a newly activated one dies before speaking',async()=>{
 const root=await install();try{
  const probation=async()=>{await mkdir(join(root,'runtime/launcher-backup/launcher'),{recursive:true});await writeFile(join(root,'runtime/launcher-backup/launcher/manual-update.mjs'),'previous-entry');await writeFile(join(root,'launcher/manual-update.mjs'),'broken-entry');await writeFile(join(root,'runtime/launcher-probation.json'),JSON.stringify({token:'t',files:['launcher/manual-update.mjs'],absent:[]}));};
  const child=(lines:string[],code:number)=>(()=>{const c:any=new EventEmitter();c.stdout=new PassThrough();c.stderr=new PassThrough();c.kill=()=>{};setImmediate(()=>{for(const l of lines)c.stdout.write(l+'\n');c.stderr.write('SyntaxError: Unexpected token');setImmediate(()=>c.emit('close',code));});return c;});
  await probation();await assert.rejects(runUpdate(root,{spawnImpl:child([],1)}),/previous launcher was restored/);
  assert.equal(await readFile(join(root,'launcher/manual-update.mjs'),'utf8'),'previous-entry');assert.equal(existsSync(join(root,'runtime/launcher-probation.json')),false);
  // An ordinary update failure (the launcher ran and reported it) keeps the new launcher.
  await probation();await assert.rejects(runUpdate(root,{spawnImpl:child(['{"type":"error","text":"offline"}'],1)}),/offline/);
  assert.equal(await readFile(join(root,'launcher/manual-update.mjs'),'utf8'),'broken-entry');assert.ok(existsSync(join(root,'runtime/launcher-probation.json')));
 }finally{await rm(root,{recursive:true,force:true});}
});

test('the desktop updater timeout counts silence, not total time, so large downloads can finish',async()=>{
 const slow=()=>{const c:any=new EventEmitter();c.stdout=new PassThrough();c.stderr=new PassThrough();c.kill=()=>{};let n=0;const tick=setInterval(()=>{if(++n<6)c.stdout.write(JSON.stringify({type:'progress',text:'Downloading update: '+n})+'\n');else{clearInterval(tick);c.stdout.write('{"type":"result","status":"installed","version":"0.2.big"}\n');c.emit('close',0);}},30);return c;};
 const seen:string[]=[];assert.deepEqual(await runUpdate('/game',{spawnImpl:slow,timeoutMs:80,onProgress:(t:string)=>seen.push(t)}),{status:'installed',version:'0.2.big'});assert.equal(seen.length,5);
 const silent=()=>{const c:any=new EventEmitter();c.stdout=new PassThrough();c.stderr=new PassThrough();c.kill=()=>c.emit('close',null);return c;};
 await assert.rejects(runUpdate('/game',{spawnImpl:silent,timeoutMs:40}),/timed out/);
});

test('schema 2 refuses path traversal, runtime/settings paths, unlisted launcher files and case duplicates',async()=>{
 const root=await install();try{
  const bad=['../evil.txt','dist/client/../../evil.txt','/abs/evil.txt','dist\\client\\evil.txt','C:/evil.txt','runtime/node.exe','settings.json','test-channel.json','launcher/evil.mjs','launcher/../evil.mjs','DIST/client/x.js'];
  for(const path of [...bad,'dist/client/A.js|dist/client/a.js']){
   const extra=path.split('|').map(p=>({path:p,data:Buffer.from('x')})),entries=[...game('0.2.evil'),...extra];
   const pack=container({schema:2,version:'0.2.evil',files:entries.map(f=>({path:f.path,size:f.data.length,sha256:hash(f.data)}))},entries.map(f=>f.data));
   const m=manifestFor(pack,'0.2.evil',entries.length,entries.reduce((n,f)=>n+f.data.length,0));
   await assert.rejects(run(root,serve({'latest-v2.json':m,[m.file]:pack})),/Invalid update path/,path);
  }
  await untouched(root);assert.equal(existsSync(join(root,'..','evil.txt')),false);assert.equal(await readFile(join(root,'runtime/node.exe'),'utf8'),'runtime');
 }finally{await rm(root,{recursive:true,force:true});}
});

test('schema 2 refuses oversize, hash-mismatched, truncated and interrupted downloads and cleans up',async()=>{
 const root=await install();try{
  const {manifest,pack}=feed('0.2.dl');
  assert.throws(()=>checkManifest2({...manifest,bytes:LIMITS.schema2Bytes+1}),/larger than this launcher accepts \(256 MiB\)/);
  await assert.rejects(applyUpdates(root,{fetchImpl:serve({'latest-v2.json':manifest,[manifest.file]:pack}),force:true,strict:true,schema2:true,maxBytes:pack.length-1,log:quiet}),/larger than this launcher accepts/);
  const flipped=Buffer.from(pack);flipped[flipped.length>>1]^=0xff;
  const interrupted=new Response(new ReadableStream({start(c){c.enqueue(new Uint8Array(pack.subarray(0,pack.length>>1)));c.error(Error('connection reset'));}}));
  for(const [body,error] of [[Buffer.concat([pack,Buffer.from('x')]),/larger than its manifest/],[flipped,/checksum mismatch/],[pack.subarray(0,pack.length-7),/incomplete/],[interrupted,/Update download failed/]] as const){
   await assert.rejects(run(root,serve({'latest-v2.json':manifest,[manifest.file]:body})),error);await untouched(root);
  }
  for(const bad of [{...manifest,schema:3},{...manifest,encoding:'gzip'},{...manifest,file:'arena-0000000000000000.pack'},{...manifest,version:'has space'},{...manifest,launcher:[{path:'launcher/evil.mjs',sha256:'0'.repeat(64)}]},{...manifest,files:LIMITS.schema2Files+1}])assert.throws(()=>checkManifest2(bad),/Unsupported update manifest/);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('schema 2 refuses a corrupt container: file hash, trailing bytes, truncation, magic and launcher list',async()=>{
 const root=await install();try{
  const entries=game('0.2.box'),index=(e=entries)=>({schema:2,version:'0.2.box',files:e.map(f=>({path:f.path,size:f.data.length,sha256:hash(f.data)}))}),size=entries.reduce((n,f)=>n+f.data.length,0),bodies=entries.map(f=>f.data);
  const cases:[Buffer,RegExp,object?][]=[
   [container({...index(),files:index().files.map((f,i)=>i===0?{...f,sha256:'0'.repeat(64)}:f)},bodies),/Invalid update file/],
   [container(index(),bodies,Buffer.from('trailing')),/Invalid update package/],
   [container(index(),bodies.slice(0,-1)),/Invalid update package/],
   [brotliCompressSync(Buffer.concat([Buffer.from('NOTAPACK'),Buffer.alloc(4)])),/Invalid update package/],
   [container(index(),bodies),/Invalid update package/,{launcher:[{path:'launcher/start.mjs',sha256:'0'.repeat(64)}]}],
  ];
  for(const [pack,error,patch] of cases){const m={...manifestFor(pack,'0.2.box',entries.length,size),...patch};await assert.rejects(run(root,serve({'latest-v2.json':m,[m.file]:pack})),error);await untouched(root);}
 }finally{await rm(root,{recursive:true,force:true});}
});

test('schema 2 installs a payload larger than the 32 MiB schema-1 cap',async()=>{
 const root=await install();try{
  const big=randomBytes(40*MiB),{manifest,files}=feed('0.2.big',{extra:[{path:'dist/client/music/big.bin',data:big}]});
  assert.ok(manifest.bytes>LIMITS.schema1Bytes,'payload exceeds what every pre-v2 launcher accepts');
  assert.equal(await run(root,serve(files)),true);assert.equal(hash(await readFile(join(root,'dist/client/music/big.bin'))),hash(big));assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved');
 }finally{await rm(root,{recursive:true,force:true});}
});

test('latest-v2.json is preferred; only a missing v2 feed falls back to schema 1',async()=>{
 const root=await install();try{
  const calls:string[]=[],{files}=feed('0.2.pref');files['latest.json']={schema:1};assert.equal(await run(root,serve(files,calls)),true);assert.deepEqual(calls,['latest-v2.json',(files['latest-v2.json'] as any).file]);
  for(const response of [new Response('down',{status:500}),new Response('<html>not json</html>'),new Response(JSON.stringify({pad:'x'.repeat(LIMITS.manifestBytes)}))]){const seen:string[]=[];await assert.rejects(run(root,serve({'latest-v2.json':response,'latest.json':{schema:1}},seen)),/unavailable|Unsupported update manifest/);assert.deepEqual(seen,['latest-v2.json'],'no fallback to schema 1');}
  // 404 falls back to the schema-1 feed exactly as before.
  const version='0.2.legacy',v1=encodeSchema1(game(version)),manifest={schema:1,nodeMajor:24,version,file:'arena-'+hash(v1.gz).slice(0,16)+'.json.gz',sha256:hash(v1.gz)},seen:string[]=[];
  const legacy=applyUpdates(root,{fetchImpl:serve({'latest.json':manifest,[manifest.file]:v1.gz},seen),force:true,strict:true,schema2:true,log:quiet});
  if(Number(process.versions.node.split('.')[0])>=24){assert.equal(await legacy,true);assert.equal(JSON.parse(await readFile(join(root,'release.json'),'utf8')).version,version);}else await assert.rejects(legacy,/newer portable runtime/);
  assert.deepEqual(seen.slice(0,2),['latest-v2.json','latest.json']);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('packager profiles: live keeps 25 MiB and schema 1 only; test-channel allows 32 MiB plus schema 2 with the channel launcher',async()=>{
 const live=packagerConfig({});assert.deepEqual([live.profile,live.schema1Max,live.schema1Overflow,live.schema2],['live',25*MiB,'fail',false]);
 const testChannel=packagerConfig({CHIP_UPDATE_PROFILE:'test-channel'});assert.deepEqual([testChannel.schema1Max,testChannel.schema1Overflow,testChannel.schema2,testChannel.schema2Max,testChannel.quality],[LIMITS.schema1Bytes,'hold',true,95*MiB,11]);
 assert.equal(packagerConfig({CHIP_UPDATE_SCHEMA1_MAX_MIB:'30'}).schema1Max,30*MiB);
 for(const env of [{CHIP_UPDATE_SCHEMA1_MAX_MIB:'33'},{CHIP_UPDATE_SCHEMA2_MAX_MIB:'257'},{CHIP_UPDATE_PROFILE:'production'},{CHIP_UPDATE_SCHEMA2_QUALITY:'12'}])assert.throws(()=>packagerConfig(env));
 const overlay=await mkdtemp(join(tmpdir(),'overlay-'));try{
  assert.throws(()=>launcherEntries({profile:'test-channel',overlay:''},repoFile('.')),/test-channel launcher\/manual-update.mjs/);
  await writeFile(join(overlay,'manual-update.mjs'),'// channel entry');await writeFile(join(overlay,'remote-feed.mjs'),'// feed');
  const entries=launcherEntries({profile:'test-channel',overlay},repoFile('.'));assert.deepEqual(entries.map(e=>e.path),['launcher/manual-update.mjs','launcher/remote-feed.mjs','launcher/start.mjs','launcher/update.mjs']);assert.equal(entries[0].data.toString(),'// channel entry');
  await writeFile(join(overlay,'evil.cjs'),'x');assert.throws(()=>launcherEntries({profile:'test-channel',overlay},repoFile('.')),/whitelist/);
 }finally{await rm(overlay,{recursive:true,force:true});}
 const v1=encodeSchema1([{path:'release.json',data:Buffer.from('{}')}]);assert.deepEqual(JSON.parse(gunzipSync(v1.gz).toString()),{schema:1,files:[{path:'release.json',sha256:hash('{}'),data:Buffer.from('{}').toString('base64')}]});
 assert.equal(schema1Problem({...v1,gz:Buffer.alloc(26*MiB)},25*MiB)?.includes('25 MiB'),true);assert.equal(schema1Problem({...v1,gz:Buffer.alloc(26*MiB)},LIMITS.schema1Bytes),null);
 assert.match(String(schema1Problem({...v1,unpacked:LIMITS.schema1Unpacked+1},LIMITS.schema1Bytes)),/unpacked/);
 assert.deepEqual((await readdir(repoFile('launcher'))).filter(f=>f.endsWith('.mjs')).every(f=>updater.LAUNCHER_FILES.includes('launcher/'+f)),true,'every shipped launcher module is self-updatable');
});
