// Updater v2: schema-2 container, launcher self-update (stage → next-start activation → rollback) and packager profiles.
import {test} from 'node:test';import assert from 'node:assert/strict';
import {mkdtemp,mkdir,writeFile,readFile,rm,copyFile,readdir} from 'node:fs/promises';import {existsSync,readFileSync,writeFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';import {join} from 'node:path';import {fileURLToPath} from 'node:url';
import {createHash,randomBytes} from 'node:crypto';import {brotliCompressSync,gunzipSync} from 'node:zlib';
import {createRequire} from 'node:module';import {EventEmitter} from 'node:events';import {PassThrough} from 'node:stream';import {spawn} from 'node:child_process';
import * as updater from '../launcher/update.mjs';
import {encodeSchema1,encodeSchema2,manifest2,packagerConfig,launcherEntries,schema1Problem,MiB} from '../scripts/update-format.mjs';
const {runUpdate,probationToken,proveLauncher,restoreLauncher,hostingProbation}=createRequire(import.meta.url)('../desktop/update.cjs');
const {applyUpdates,startLauncher,activateLauncher,recoverLauncher,checkManifest2,LIMITS,UPDATE_BASE}=updater;
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
  // Silent phases report progress so the desktop updater's 90 s no-output timeout never fires mid-install.
  for(const step of [/^Downloading update/,/^Unpacking and checking \d+ files/,/^Checking launcher\/update\.mjs/,/^Installing update files/])assert.ok(logs.some(m=>step.test(m)),String(step));
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
  // The activating run cannot prove its own launcher: desktop/update.cjs saw no probation before it started.
  assert.equal(await fresh.applyUpdates(root,{fetchImpl:serve(files),force:true,strict:true,schema2:true,log:quiet}),false);
  assert.equal(proveLauncher(root,'update',null),false);assert.ok(existsSync(join(root,'runtime/launcher-probation.json')));
  // Later processes: an Update that installs proves update.mjs/manual-update.mjs for Update; update.mjs also runs hosting.
  const token=probationToken(root);assert.equal(typeof token,'string');
  assert.equal(await run(root,serve(feed('0.2.v2next',{launcher:[{path:'launcher/manual-update.mjs',data:manual},{path:'launcher/update.mjs',data:next}]}).files)),true);
  assert.equal(proveLauncher(root,'update',token),true);assert.deepEqual(JSON.parse(await readFile(join(root,'runtime/launcher-probation.json'),'utf8')).proven,['update']);
  assert.equal(restoreLauncher(root,'update'),false,'a proven kind is no longer rolled back');assert.ok(existsSync(join(root,'runtime/launcher-backup/launcher/update.mjs')));
  assert.equal(hostingProbation(root).ready(),true);assert.equal(existsSync(join(root,'runtime/launcher-probation.json')),false);assert.equal(existsSync(join(root,'runtime/launcher-backup')),false);
  assert.equal(await readFile(join(root,'launcher/update.mjs'),'utf8'),next.toString());assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved');
 }finally{await rm(root,{recursive:true,force:true});}
});

test('a staged launcher that the feed no longer lists is dropped instead of activated',async()=>{
 const root=await install();try{
  const start=marker(await readFile(repoFile('launcher/start.mjs'),'utf8'),'withdrawn');
  assert.equal(await run(root,serve(feed('0.2.same',{launcher:[{path:'launcher/start.mjs',data:start}]}).files)),true);assert.ok(existsSync(join(root,'runtime/launcher-pending/pending.json')));
  // The channel then republishes the same game version without that launcher change.
  assert.equal(await run(root,serve(feed('0.2.same').files)),false);assert.equal(existsSync(join(root,'runtime/launcher-pending')),false);
  assert.equal(await startLauncher(root,{log:quiet}),updater);assert.equal(await readFile(join(root,'launcher/start.mjs'),'utf8'),await readFile(repoFile('launcher/start.mjs'),'utf8'));
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
  const logs:string[]=[];assert.equal(await activateLauncher(root,{log:m=>logs.push(m)}),false);assert.equal(logs.at(-1),'Discarded an invalid staged launcher update.');
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

const fakeChild=(lines:string[],code:number,stderr='',during?:()=>void)=>(()=>{const c:any=new EventEmitter();c.stdout=new PassThrough();c.stderr=new PassThrough();c.kill=()=>{};setImmediate(()=>{during?.();for(const l of lines)c.stdout.write(l+'\n');if(stderr)c.stderr.write(stderr);setImmediate(()=>c.emit('close',code));});return c;});
async function onProbation(root:string,files:string[],extra:Record<string,unknown>={}){
 await mkdir(join(root,'runtime/launcher-backup/launcher'),{recursive:true});
 for(const path of files){await writeFile(join(root,'runtime/launcher-backup',path),'previous '+path);await writeFile(join(root,path),'new '+path);}
 await mkdir(join(root,'runtime/launcher-pending/launcher'),{recursive:true});await writeFile(join(root,'runtime/launcher-pending/pending.json'),'{}');
 await writeFile(join(root,'runtime/launcher-probation.json'),JSON.stringify({token:'t',files,absent:[],...extra}));
}
const restored=async(root:string,path:string)=>(await readFile(join(root,path),'utf8'))==='previous '+path&&!existsSync(join(root,'runtime/launcher-probation.json'))&&!existsSync(join(root,'runtime/launcher-backup'))&&!existsSync(join(root,'runtime/launcher-pending'));
test('the desktop updater restores the previous launcher after any failed Update while the new one is unproven',async()=>{
 const root=await install();try{
  // An entry point that dies before speaking, and one that loads, speaks and fails (for example a broken feed mapping).
  for(const [lines,stderr] of [[[],'SyntaxError: Unexpected token'],[['{"type":"progress","text":"Launcher updated."}','{"type":"error","text":"Update service unavailable"}'],'']] as const){
   await onProbation(root,['launcher/manual-update.mjs']);
   const error=await runUpdate(root,{spawnImpl:fakeChild([...lines],1,stderr)}).then(()=>null,(e:Error)=>e.message);
   assert.match(String(error),/previous launcher was restored/);assert.ok(await restored(root,'launcher/manual-update.mjs'),'restored, backup and staged launcher dropped');
  }
  assert.match(String(await runUpdate(root,{spawnImpl:fakeChild(['{"type":"error","text":"Update service unavailable"}'],1)}).then(()=>null,(e:Error)=>e.message)),/^Update service unavailable$/,'no probation: an ordinary failure');
  // "Current" proves nothing; only an installed update run entirely by the probation launcher proves Update.
  await onProbation(root,['launcher/manual-update.mjs']);
  assert.deepEqual(await runUpdate(root,{spawnImpl:fakeChild(['{"type":"result","status":"current","version":"0.2.same"}'],0)}),{status:'current',version:'0.2.same'});assert.ok(existsSync(join(root,'runtime/launcher-probation.json')));
  const activatedDuringRun=()=>void writeFileSync(join(root,'runtime/launcher-probation.json'),JSON.stringify({token:'newer',files:['launcher/manual-update.mjs'],absent:[]}));
  await runUpdate(root,{spawnImpl:fakeChild(['{"type":"result","status":"installed","version":"0.2.next"}'],0,'',activatedDuringRun)});assert.equal(probationToken(root),'newer','a run that activated a newer launcher proves nothing');
  await runUpdate(root,{spawnImpl:fakeChild(['{"type":"result","status":"installed","version":"0.2.next"}'],0)});
  assert.equal(existsSync(join(root,'runtime/launcher-probation.json')),false);assert.equal(existsSync(join(root,'runtime/launcher-backup')),false);assert.equal(await readFile(join(root,'launcher/manual-update.mjs'),'utf8'),'new launcher/manual-update.mjs');
  // Once Update is proven, a later failure (for example offline) keeps the new launcher; hosting is still on probation.
  await onProbation(root,['launcher/update.mjs'],{proven:['update']});
  await assert.rejects(runUpdate(root,{spawnImpl:fakeChild(['{"type":"error","text":"offline"}'],1)}),/^Error: offline$/);assert.equal(await readFile(join(root,'launcher/update.mjs'),'utf8'),'new launcher/update.mjs');
  assert.equal(hostingProbation(root).failed(),true);assert.ok(await restored(root,'launcher/update.mjs'));
  // A crash in the middle of an activation (journal left behind) is restored by any failed run.
  await onProbation(root,['launcher/start.mjs']);await rm(join(root,'runtime/launcher-probation.json'));await writeFile(join(root,'runtime/launcher-journal.json'),JSON.stringify({token:'x',files:['launcher/start.mjs','../settings.json'],absent:[]}));
  await assert.rejects(runUpdate(root,{spawnImpl:fakeChild([],1,'boom')}),/previous launcher was restored/);assert.ok(await restored(root,'launcher/start.mjs'));assert.equal(existsSync(join(root,'runtime/launcher-journal.json')),false);
  assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved');
 }finally{await rm(root,{recursive:true,force:true});}
});

test('hosting proves a new start.mjs at "YOUR GAME:" and restores it when it fails before that',async()=>{
 const root=await install();try{
  await onProbation(root,['launcher/start.mjs']);
  assert.equal(restoreLauncher(root,'update'),false,'start.mjs alone is not part of Update');
  const activating=(()=>{const saved=readFileSync(join(root,'runtime/launcher-probation.json'));rmSync(join(root,'runtime/launcher-probation.json'));const h=hostingProbation(root);writeFileSync(join(root,'runtime/launcher-probation.json'),saved);return h;})();
  assert.equal(activating.ready(),false,'the hosting run that activated the launcher ran the previous start.mjs');
  assert.equal(hostingProbation(root).failed(),true);assert.ok(await restored(root,'launcher/start.mjs'));
  await onProbation(root,['launcher/start.mjs']);assert.equal(hostingProbation(root).ready(),true);
  assert.equal(existsSync(join(root,'runtime/launcher-probation.json')),false);assert.equal(existsSync(join(root,'runtime/launcher-backup')),false);assert.equal(hostingProbation(root).failed(),false);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('a newer launcher activated while one is on probation keeps the last proven backup',async()=>{
 const root=await install();try{
  const original={start:await readFile(repoFile('launcher/start.mjs'),'utf8'),manual:await readFile(repoFile('launcher/manual-update.mjs'),'utf8')};
  const start1=marker(original.start,'start v1'),start2=marker(original.start,'start v2'),manual2=marker(original.manual,'manual v2');
  assert.equal(await run(root,serve(feed('0.2.p1',{launcher:[{path:'launcher/start.mjs',data:start1}]}).files)),true);assert.equal(await activateLauncher(root,{log:quiet}),true);
  assert.equal(await run(root,serve(feed('0.2.p2',{launcher:[{path:'launcher/start.mjs',data:start2},{path:'launcher/manual-update.mjs',data:manual2}]}).files)),true);assert.equal(await activateLauncher(root,{log:quiet}),true);
  assert.equal(await readFile(join(root,'launcher/start.mjs'),'utf8'),start2.toString());
  const record=JSON.parse(await readFile(join(root,'runtime/launcher-probation.json'),'utf8'));assert.deepEqual(record.files.sort(),['launcher/manual-update.mjs','launcher/start.mjs']);assert.equal(record.proven,undefined);
  assert.equal(hostingProbation(root).failed(),true);
  assert.equal(await readFile(join(root,'launcher/start.mjs'),'utf8'),original.start,'back to the launcher before both unproven updates');assert.equal(await readFile(join(root,'launcher/manual-update.mjs'),'utf8'),original.manual);
 }finally{await rm(root,{recursive:true,force:true});}
});

// A desktop-style entry point that reads a local feed directory, so real child processes exercise the whole Update path.
const LOCAL_ENTRY=`import * as installed from './update.mjs';
import {readFile} from 'node:fs/promises';import {dirname,join,resolve} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..'),send=m=>process.stdout.write(JSON.stringify(m)+'\\n');
try{const log=text=>send({type:'progress',text}),updater=await installed.startLauncher(root,{log});
 const fetchImpl=async url=>{try{return new Response(await readFile(join(process.env.CHIP_TEST_FEED,url.slice(updater.UPDATE_BASE.length))));}catch{return new Response('missing',{status:404});}};
 const result=await updater.applyUpdates(root,{force:true,strict:true,schema2:true,fetchImpl,log});
 send({type:'result',status:result?'installed':'current',version:JSON.parse(await readFile(join(root,'release.json'),'utf8')).version});
}catch(error){send({type:'error',text:error.message});process.exitCode=1;}
`;
test('a new update.mjs that loads but whose update path fails is restored by the next desktop Update; a fixed publish then installs',async()=>{
 const root=await install(),dir=await mkdtemp(join(tmpdir(),'chip-feed-'));try{
  await writeFile(join(root,'launcher/manual-update.mjs'),LOCAL_ENTRY);
  const publish=async(files:Record<string,unknown>)=>{for(const f of await readdir(dir))await rm(join(dir,f));for(const [name,value] of Object.entries(files))await writeFile(join(dir,name),Buffer.isBuffer(value)?value:JSON.stringify(value));};
  const update=()=>runUpdate(root,{spawnImpl:(exe:string,args:string[],options:any)=>spawn(process.execPath,args,{...options,env:{...process.env,CHIP_TEST_FEED:dir}}),timeoutMs:60000}).then((r:any)=>({ok:true,...r}),(e:Error)=>({ok:false,error:e.message}));
  const broken=Buffer.from(SOURCE.replace('export async function applyUpdates(root,{','export async function applyUpdates(){throw Error(\'Update service unavailable\');}\nasync function unused(root,{'));
  assert.notEqual(broken.toString(),SOURCE);
  await publish(feed('0.2.s1staged',{launcher:[{path:'launcher/update.mjs',data:broken}]}).files);
  assert.deepEqual(await update(),{ok:true,status:'installed',version:'0.2.s1staged'});assert.ok(existsSync(join(root,'runtime/launcher-pending/pending.json')));
  const failed:any=await update();
  assert.equal(failed.ok,false);assert.match(failed.error,/^Update service unavailable\. .*previous launcher was restored/);
  assert.equal(await readFile(join(root,'launcher/update.mjs'),'utf8'),SOURCE);
  for(const left of ['launcher-probation.json','launcher-backup','launcher-pending','launcher-journal.json'])assert.equal(existsSync(join(root,'runtime',left)),false,left);
  await publish(feed('0.2.s1fixed',{launcher:[{path:'launcher/update.mjs',data:Buffer.from(SOURCE)}]}).files);
  assert.deepEqual(await update(),{ok:true,status:'installed',version:'0.2.s1fixed'});assert.equal(existsSync(join(root,'runtime/launcher-pending')),false);
  assert.equal(await readFile(join(root,'settings.json'),'utf8'),'saved');assert.equal(await readFile(join(root,'runtime/node.exe'),'utf8'),'runtime');
 }finally{await rm(root,{recursive:true,force:true});await rm(dir,{recursive:true,force:true});}
});

test('the desktop updater timeout counts silence, not total time, so large downloads can finish',async()=>{
 const slow=()=>{const c:any=new EventEmitter();c.stdout=new PassThrough();c.stderr=new PassThrough();c.kill=()=>{};let n=0;const tick=setInterval(()=>{if(++n<6)c.stdout.write(JSON.stringify({type:'progress',text:'Downloading update: '+n})+'\n');else{clearInterval(tick);c.stdout.write('{"type":"result","status":"installed","version":"0.2.big"}\n');c.emit('close',0);}},30);return c;};
 const seen:string[]=[];assert.deepEqual(await runUpdate('/game',{spawnImpl:slow,timeoutMs:80,onProgress:(t:string)=>seen.push(t)}),{status:'installed',version:'0.2.big'});assert.equal(seen.length,5);
 const silent=()=>{const c:any=new EventEmitter();c.stdout=new PassThrough();c.stderr=new PassThrough();c.kill=()=>c.emit('close',null);return c;};
 await assert.rejects(runUpdate('/game',{spawnImpl:silent,timeoutMs:40}),/timed out/);
 const chatty=()=>{const c:any=new EventEmitter();c.stdout=new PassThrough();c.stderr=new PassThrough();c.kill=()=>setImmediate(()=>c.emit('close',null));setImmediate(()=>c.stdout.write('x'.repeat(70000)));return c;};
 await assert.rejects(runUpdate('/game',{spawnImpl:chatty}),/Unexpected updater response/);
});

test('schema 2 refuses path traversal, runtime/settings paths, unlisted launcher files, case duplicates and Windows-unsafe names',async()=>{
 const root=await install();try{
  const bad=['../evil.txt','dist/client/../../evil.txt','/abs/evil.txt','dist\\client\\evil.txt','C:/evil.txt','runtime/node.exe','settings.json','test-channel.json','launcher/evil.mjs','launcher/../evil.mjs','DIST/client/x.js','dist/client/CON','dist/client/nul.txt','dist/client/com1/x.js','dist/client/index.html.','dist/client/a ','dist/client/a?.js'];
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

test('a v2 launcher never installs the schema-1 hold notice; a schema-1 install drops a launcher staged by schema 2',async()=>{
 const root=await install();try{
  const start=marker(await readFile(repoFile('launcher/start.mjs'),'utf8'),'staged');
  assert.equal(await run(root,serve(feed('0.2.staged',{launcher:[{path:'launcher/start.mjs',data:start}]}).files)),true);assert.ok(existsSync(join(root,'runtime/launcher-pending/pending.json')));
  const schema1=(version:string,extra:object={})=>{const v1=encodeSchema1(game(version)),sha256=hash(v1.gz),file='arena-'+sha256.slice(0,16)+'.json.gz';return {'latest.json':{schema:1,nodeMajor:24,build:'c'.repeat(24),version,file,sha256,bytes:v1.gz.length,...extra},[file]:v1.gz};};
  // latest-v2.json missing (404) while latest.json is the notice for old installs: refuse instead of downgrading.
  await assert.rejects(run(root,serve(schema1('0.2.held-download-the-new-TEST-ZIP-from-GitHub-Releases',{hold:true}))),/publishing a new update/);
  assert.equal(JSON.parse(await readFile(join(root,'release.json'),'utf8')).version,'0.2.staged');
  const legacy=run(root,serve(schema1('0.2.legacy1')));
  if(Number(process.versions.node.split('.')[0])>=24){assert.equal(await legacy,true);assert.equal(existsSync(join(root,'runtime/launcher-pending')),false,'schema 1 carries no launcher');assert.equal(await startLauncher(root,{log:quiet}),updater);}
  else await assert.rejects(legacy,/newer portable runtime/);
 }finally{await rm(root,{recursive:true,force:true});}
});

test('packager profiles: live keeps 25 MiB and schema 1 only; test-channel allows 32 MiB plus schema 2 with the channel launcher',async()=>{
 const live=packagerConfig({});assert.deepEqual([live.profile,live.schema1Max,live.schema1Overflow,live.schema2],['live',25*MiB,'fail',false]);
 const testChannel=packagerConfig({CHIP_UPDATE_PROFILE:'test-channel'});assert.deepEqual([testChannel.schema1Max,testChannel.schema1Overflow,testChannel.schema2,testChannel.schema2Max,testChannel.quality],[LIMITS.schema1Bytes,'hold',true,95*MiB,11]);
 assert.equal(packagerConfig({CHIP_UPDATE_PROFILE:'test-channel',CHIP_UPDATE_SCHEMA1_MAX_MIB:'30'}).schema1Max,30*MiB);assert.equal(packagerConfig({CHIP_UPDATE_SCHEMA1_MAX_MIB:'20'}).schema1Max,20*MiB);
 // Live can only lower its 25 MiB limits: the Site cannot serve a larger file.
 for(const env of [{CHIP_UPDATE_SCHEMA1_MAX_MIB:'30'},{CHIP_UPDATE_SCHEMA2:'1',CHIP_UPDATE_SCHEMA2_MAX_MIB:'26'},{CHIP_UPDATE_PROFILE:'test-channel',CHIP_UPDATE_SCHEMA1_MAX_MIB:'33'},{CHIP_UPDATE_SCHEMA2_MAX_MIB:'257'},{CHIP_UPDATE_PROFILE:'production'},{CHIP_UPDATE_SCHEMA2_QUALITY:'12'}])assert.throws(()=>packagerConfig(env));
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
 // desktop/update.cjs keeps its own copy of the whitelist for the stable rollback; both must agree.
 const desktopList=JSON.parse((await readFile(repoFile('desktop/update.cjs'),'utf8')).match(/const LAUNCHER_FILES=(\[[^\]]*\])/)![1].replaceAll("'",'"'));
 assert.deepEqual(desktopList,[...updater.LAUNCHER_FILES]);
});
