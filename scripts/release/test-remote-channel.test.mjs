// Test tooling regression: the remote test channel maps only feed files, never falls back to production,
// prefers latest-v2.json only for updater-v2 installs, and the schema-1 hold keeps old installs informed.
import {test} from 'node:test';import assert from 'node:assert/strict';
import {remoteFeed} from './remote-feed.mjs';
import {holdSchema1,NOTICE} from './schema1-hold.mjs';
import {createRequire} from 'node:module';
import {mkdtemp,writeFile,readFile,mkdir,readdir} from 'node:fs/promises';import {join} from 'node:path';import {tmpdir} from 'node:os';
import {createHash} from 'node:crypto';import {gzipSync,gunzipSync,brotliCompressSync} from 'node:zlib';import {execFileSync,spawnSync} from 'node:child_process';
import {feedProblems,privatePath} from './source-privacy.mjs';
const require=createRequire(import.meta.url);const {createRemoteStatusHandler}=require('./test-remote-status.cjs');const {CHECK_URL}=require('./test-update-status.cjs');
const BASE='https://chip-arena-prototype.sheldon37.chatgpt.site/updates/',CH='https://raw.githubusercontent.com/o/r/main/feed/';
const python=process.env.CHIP_PYTHON||'python3',hash=b=>createHash('sha256').update(b).digest('hex');
test('remote feed maps only manifest and payload names onto the channel',async()=>{
 const seen=[];const f=remoteFeed(CH,BASE,async(url,o)=>{seen.push([url,o.cache]);return new Response('x');});
 await f(BASE+'latest.json');await f(BASE+'arena-0123456789abcdef.json.gz');await f(BASE+'latest-v2.json');await f(BASE+'arena-0123456789abcdef.pack');
 assert.match(seen[0][0],/^https:\/\/raw\.githubusercontent\.com\/o\/r\/main\/feed\/latest\.json\?t=\d+$/);assert.equal(seen[1][0],CH+'arena-0123456789abcdef.json.gz');
 assert.match(seen[2][0],/feed\/latest-v2\.json\?t=\d+$/);assert.equal(seen[3][0],CH+'arena-0123456789abcdef.pack');assert.ok(seen.every(s=>s[1]==='no-store'));
 for(const bad of [BASE+'../x',BASE+'server.mjs','https://evil.example/updates/latest.json',BASE+'latest-v3.json',BASE+'arena-0123.pack',BASE+'arena-0123456789abcdef.pack/../x'])assert.throws(()=>f(bad));
 assert.throws(()=>remoteFeed('http://insecure/',BASE));assert.throws(()=>remoteFeed('https://no-slash',BASE));
});
test('status handler reports the channel manifest, forwards other requests, and never uses production',async()=>{
 const root=await mkdtemp(join(tmpdir(),'chan-'));await writeFile(join(root,'test-channel.json'),JSON.stringify({base:CH}));
 const calls=[];let manifest={build:'a'.repeat(24),version:'0.2.test'};
 const h=createRemoteStatusHandler(root,async(req,opt)=>{calls.push([req.url,opt?.bypassCustomProtocolHandlers]);if(req.url.startsWith(CH+'latest.json'))return manifest?Response.json(manifest):new Response('no',{status:404});if(req.url.startsWith(CH))return new Response('no',{status:404});return new Response('passthrough');});
 const ok=await(await h(new Request(CHECK_URL))).json();assert.deepEqual(ok,{build:'a'.repeat(24),version:'0.2.test'});
 assert.ok(!calls.some(c=>c[0].includes('latest-v2.json')),'a pre-v2 install never reads latest-v2.json');
 assert.equal(await(await h(new Request('https://chip-arena-prototype.sheldon37.chatgpt.site/api/accounts'))).text(),'passthrough');
 manifest=null;const down=await h(new Request(CHECK_URL));assert.equal(down.status,503);
 assert.ok(calls.every(c=>c[1]===true));assert.ok(!calls.some(c=>c[0].startsWith(BASE)));
});
test('updater-v2 installs read latest-v2.json for the badge; only a missing v2 feed falls back to latest.json',async()=>{
 const root=await mkdtemp(join(tmpdir(),'chan2-'));await writeFile(join(root,'test-channel.json'),JSON.stringify({base:CH,schema2:true}));
 let v2={status:200,body:{build:'c'.repeat(24),version:'0.2.v2'}};const v1={build:'a'.repeat(24),version:'0.2.v1-download-the-new-TEST-ZIP-from-GitHub-Releases'};
 const h=createRemoteStatusHandler(root,async req=>req.url.startsWith(CH+'latest-v2.json')?(v2.status===200?Response.json(v2.body):new Response('x',{status:v2.status})):req.url.startsWith(CH+'latest.json')?Response.json(v1):new Response('passthrough'));
 assert.deepEqual(await(await h(new Request(CHECK_URL))).json(),v2.body);
 v2={status:404};assert.deepEqual(await(await h(new Request(CHECK_URL))).json(),v1);
 v2={status:500};assert.equal((await h(new Request(CHECK_URL))).status,503);
});
async function fakeDesktop(dir,updateSource){
 const py=`import zipfile,json,sys
d=sys.argv[1]
with zipfile.ZipFile(d+'/Chip-Arena-Desktop.zip','w') as z:
 p='Chip-Arena-Desktop/'
 z.writestr(p+'resources/app/package.json',json.dumps({'main':'main.cjs'}))
 z.writestr(p+'resources/game/launcher/manual-update.mjs','orig')
 z.writestr(p+'resources/game/launcher/update.mjs',sys.argv[2])
 z.writestr(p+'resources/game/launcher/start.mjs','S')
 z.writestr(p+'resources/game/release.json',json.dumps({'version':'x'}))`;
 execFileSync(python,['-c',py,dir,updateSource]);await mkdir(join(dir,'feed'),{recursive:true});await writeFile(join(dir,'feed','latest.json'),'{}');await mkdir(join(dir,'src','launcher'),{recursive:true});await writeFile(join(dir,'src','launcher','update.mjs'),updateSource);
}
const pkg=new URL('./make-test-package.py',import.meta.url).pathname;
const read=(zip,name)=>execFileSync(python,['-c','import zipfile,sys;sys.stdout.buffer.write(zipfile.ZipFile(sys.argv[1]).read("Chip-Arena-TEST-ONLY/"+sys.argv[2]))',zip,name]);
const list=zip=>execFileSync(python,['-c','import zipfile,sys;print("\\n".join(zipfile.ZipFile(sys.argv[1]).namelist()))',zip]).toString();
test('packager wires the remote channel files only when --channel is given',async()=>{
 const dir=await mkdtemp(join(tmpdir(),'pkg-'));await fakeDesktop(dir,'U');
 execFileSync(python,[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'remote.zip'),join(dir,'src'),'--channel',CH]);
 execFileSync(python,[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'local.zip'),join(dir,'src')]);
 const r=list(join(dir,'remote.zip')),l=list(join(dir,'local.zip'));
 assert.match(r,/test-channel\.json/);assert.match(r,/test-remote-bootstrap\.cjs/);assert.match(read(join(dir,'remote.zip'),'resources/game/launcher/manual-update.mjs').toString(),/remoteFeed/);assert.match(r,/test-update-status\.cjs/);
 assert.deepEqual(JSON.parse(read(join(dir,'remote.zip'),'resources/game/test-channel.json')),{base:CH,schema2:false},'a pre-v2 launcher keeps reading schema 1 only');
 assert.doesNotMatch(l,/test-channel\.json|remote-feed/);assert.equal(JSON.parse(read(join(dir,'local.zip'),'resources/app/package.json')).main,'test-bootstrap.cjs');
 assert.throws(()=>execFileSync(python,[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'bad.zip'),join(dir,'src'),'--channel','http://x/'],{stdio:'pipe'}));
});
test('an updater-v2 seed enables schema 2 and must already hold the launcher files schema 2 delivers',async()=>{
 const dir=await mkdtemp(join(tmpdir(),'pkg2-'));const update='export async function startLauncher(){}';await fakeDesktop(dir,update);
 const channelEntry=await readFile(new URL('./test-remote-update.mjs',import.meta.url)),feedModule=await readFile(new URL('./remote-feed.mjs',import.meta.url));
 const manifest=launcher=>writeFile(join(dir,'feed','latest-v2.json'),JSON.stringify({schema:2,launcher}));
 const good=[{path:'launcher/manual-update.mjs',sha256:hash(channelEntry)},{path:'launcher/remote-feed.mjs',sha256:hash(feedModule)},{path:'launcher/start.mjs',sha256:hash('S')},{path:'launcher/update.mjs',sha256:hash(update)}];
 await manifest(good);execFileSync(python,[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'v2.zip'),join(dir,'src'),'--channel',CH]);
 assert.deepEqual(JSON.parse(read(join(dir,'v2.zip'),'resources/game/test-channel.json')),{base:CH,schema2:true});assert.match(read(join(dir,'v2.zip'),'TEST-ONLY.txt').toString(),/Updater v2/);
 // A schema-2 payload carrying the production manual-update.mjs (reads the live feed) is refused.
 await manifest([{...good[0],sha256:hash('orig')},...good.slice(1)]);
 assert.throws(()=>execFileSync(python,[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'bad.zip'),join(dir,'src'),'--channel',CH],{stdio:'pipe'}));
});
test('schema-1 hold turns the last fitting payload into a reinstall notice for pre-v2 installs',async()=>{
 const dir=await mkdtemp(join(tmpdir(),'hold-'));const channel=join(dir,'channel'),candidate=join(dir,'candidate'),out=join(dir,'out'),again=join(dir,'again');
 await mkdir(channel);await mkdir(candidate);
 const release=Buffer.from(JSON.stringify({version:'0.2.lastfit00000',channel:'stable',schema:1,nodeMajor:24})),client=Buffer.from('<html>');
 const files=[{path:'dist/client/index.html',sha256:hash(client),data:client.toString('base64')},{path:'release.json',sha256:hash(release),data:release.toString('base64')}];
 const gz=gzipSync(JSON.stringify({schema:1,files})),file='arena-'+hash(gz).slice(0,16)+'.json.gz';
 await writeFile(join(channel,file),gz);await writeFile(join(channel,'latest.json'),JSON.stringify({schema:1,nodeMajor:24,build:'a'.repeat(24),version:'0.2.lastfit00000',file,sha256:hash(gz),bytes:gz.length}));
 await writeFile(join(candidate,'latest-v2.json'),JSON.stringify({schema:2,build:'c'.repeat(24),version:'0.2.toolarge000'}));
 const {manifest,reused}=await holdSchema1(channel,candidate,out);
 assert.equal(reused,false);assert.equal(manifest.version,'0.2.lastfit00000'+NOTICE);assert.match(manifest.version,/^[\w.-]{1,80}$/);assert.equal(manifest.build,'c'.repeat(24),'old badges see a newer build');assert.equal(manifest.hold,true,'v2 launchers refuse the notice');
 const payload=JSON.parse(gunzipSync(await readFile(join(out,manifest.file))));assert.equal(hash(await readFile(join(out,manifest.file))),manifest.sha256);
 assert.equal(payload.files[0].sha256,files[0].sha256,'game files are the last fitting version');
 const notice=JSON.parse(Buffer.from(payload.files[1].data,'base64'));assert.equal(notice.version,manifest.version);assert.equal(payload.files[1].sha256,hash(Buffer.from(payload.files[1].data,'base64')));
 // Re-running on a channel that already shows the notice only moves the badge to the newest build.
 await writeFile(join(candidate,'latest-v2.json'),JSON.stringify({schema:2,build:'d'.repeat(24),version:'0.2.toolarge001'}));
 const second=await holdSchema1(out,candidate,again);assert.equal(second.reused,true);assert.equal(second.manifest.file,manifest.file);assert.equal(second.manifest.build,'d'.repeat(24));assert.equal(second.manifest.hold,true);assert.deepEqual((await readdir(again)).sort(),[manifest.file,'latest.json'].sort());
 await writeFile(join(candidate,'latest.json'),'{}');await assert.rejects(holdSchema1(channel,candidate,join(dir,'x')),/publish it normally/);
});

test('TEST-ONLY seed ZIPs and feeds never carry the editable source (chip-arena-source.zip, src/, source maps)',async()=>{
 const dir=await mkdtemp(join(tmpdir(),'privacy-'));const update='export async function startLauncher(){}';await fakeDesktop(dir,update);
 // The real desktop ZIP has the source archive at its root and in dist/client, and its READ-ME points to it.
 execFileSync(python,['-c',`import zipfile,sys
with zipfile.ZipFile(sys.argv[1],'a') as z:
 p='Chip-Arena-Desktop/'
 z.writestr(p+'chip-arena-source.zip',b'PK source')
 z.writestr(p+'resources/game/dist/client/chip-arena-source.zip',b'PK source')
 z.writestr(p+'resources/game/dist/client/index.html','<html>')
 z.writestr(p+'READ-ME.txt','Editable source: chip-arena-source.zip. Desktop source: desktop/.')`,join(dir,'Chip-Arena-Desktop.zip')]);
 const checker=new URL('./source_privacy.py',import.meta.url).pathname,scan=zip=>spawnSync(python,[checker,zip],{encoding:'utf8'});
 const desktop=scan(join(dir,'Chip-Arena-Desktop.zip'));assert.equal(desktop.status,1);assert.match(desktop.stdout,/Chip-Arena-Desktop\/chip-arena-source\.zip/);assert.match(desktop.stdout,/dist\/client\/chip-arena-source\.zip/);
 for(const args of [[],['--channel',CH]]){
  const out=join(dir,'seed'+args.length+'.zip');execFileSync(python,[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),out,join(dir,'src'),...args]);
  assert.doesNotMatch(list(out),/chip-arena-source|(^|\/)src\//m);assert.equal(scan(out).status,0,scan(out).stdout);
  assert.equal(read(out,'resources/game/dist/client/index.html').toString(),'<html>','the game files stay');
  assert.match(read(out,'READ-ME.txt').toString(),/Editable source: not included in TEST-ONLY builds\./);
 }
 // The checker also finds a renamed archive by its src/ content, a src/ folder and source maps.
 execFileSync(python,['-c',`import zipfile,io,sys
inner=io.BytesIO()
with zipfile.ZipFile(inner,'w') as z:z.writestr('chip-arena/src/server/index.ts','secret')
with zipfile.ZipFile(sys.argv[1],'w') as z:
 z.writestr('Chip-Arena-TEST-ONLY/extras.zip',inner.getvalue())
 z.writestr('Chip-Arena-TEST-ONLY/resources/game/src/game.ts','x')
 z.writestr('Chip-Arena-TEST-ONLY/resources/game/dist/client/assets/index.js.map','{}')
 z.writestr('Chip-Arena-TEST-ONLY/resources/game/dist/client/assets/source.js','ok')`,join(dir,'leaky.zip')]);
 const leaky=scan(join(dir,'leaky.zip'));assert.equal(leaky.status,1);for(const hit of ['extras.zip!chip-arena/src/server/index.ts','resources/game/src/game.ts','index.js.map'])assert.ok(leaky.stdout.includes(hit),hit);assert.ok(!leaky.stdout.includes('source.js'));
 // Feeds: both schemas are decoded and every payload path is checked.
 for(const path of ['dist/client/chip-arena-source.zip','dist/client/other.zip','dist/client/src/main.ts','drizzle/x.css.map'])assert.equal(privatePath(path),true,path);
 for(const path of ['dist/client/index.html','dist/client/assets/source-B1.js','server.mjs','dist/client/srcset.png','release.json'])assert.equal(privatePath(path),false,path);
 const feedDir=async(name,paths)=>{
  const f=join(dir,name);await mkdir(f);const files=paths.map(p=>({path:p,data:Buffer.from('x '+p)}));
  const gz=gzipSync(JSON.stringify({schema:1,files:files.map(x=>({path:x.path,sha256:hash(x.data),data:x.data.toString('base64')}))}));await writeFile(join(f,'arena-'+hash(gz).slice(0,16)+'.json.gz'),gz);await writeFile(join(f,'latest.json'),JSON.stringify({schema:1,file:'arena-'+hash(gz).slice(0,16)+'.json.gz'}));
  const index=Buffer.from(JSON.stringify({schema:2,files:files.map(x=>({path:x.path,size:x.data.length,sha256:hash(x.data)}))})),head=Buffer.alloc(12);head.write('CHIPPACK',0,'latin1');head.writeUInt32BE(index.length,8);
  const pack=brotliCompressSync(Buffer.concat([head,index,...files.map(x=>x.data)]));await writeFile(join(f,'arena-'+hash(pack).slice(0,16)+'.pack'),pack);await writeFile(join(f,'latest-v2.json'),JSON.stringify({schema:2,file:'arena-'+hash(pack).slice(0,16)+'.pack'}));
  return f;
 };
 assert.deepEqual(feedProblems(await feedDir('clean',['server.mjs','dist/client/index.html','release.json'])),[]);
 assert.deepEqual(feedProblems(await feedDir('leaky-feed',['dist/client/index.html','dist/client/chip-arena-source.zip','dist/client/src/main.ts'])),['latest.json: dist/client/chip-arena-source.zip','latest.json: dist/client/src/main.ts','latest-v2.json: dist/client/chip-arena-source.zip','latest-v2.json: dist/client/src/main.ts']);
 const cli=spawnSync(process.execPath,[new URL('./source-privacy.mjs',import.meta.url).pathname,join(dir,'leaky-feed')],{encoding:'utf8'});assert.equal(cli.status,1);assert.match(cli.stdout,/source content/);
});
