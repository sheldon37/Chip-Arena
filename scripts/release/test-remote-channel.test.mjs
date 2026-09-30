// Test tooling regression: the remote test channel maps only feed files, never falls back to production.
import {test} from 'node:test';import assert from 'node:assert/strict';
import {remoteFeed} from './remote-feed.mjs';
import {createRequire} from 'node:module';
import {mkdtemp,writeFile,readFile} from 'node:fs/promises';import {join} from 'node:path';import {tmpdir} from 'node:os';
const require=createRequire(import.meta.url);const {createRemoteStatusHandler}=require('./test-remote-status.cjs');const {CHECK_URL}=require('./test-update-status.cjs');
const BASE='https://chip-arena-prototype.sheldon37.chatgpt.site/updates/',CH='https://raw.githubusercontent.com/o/r/main/feed/';
test('remote feed maps only manifest and payload names onto the channel',async()=>{
 const seen=[];const f=remoteFeed(CH,BASE,async(url,o)=>{seen.push([url,o.cache]);return new Response('x');});
 await f(BASE+'latest.json');await f(BASE+'arena-0123456789abcdef.json.gz');
 assert.match(seen[0][0],/^https:\/\/raw\.githubusercontent\.com\/o\/r\/main\/feed\/latest\.json\?t=\d+$/);assert.equal(seen[1][0],CH+'arena-0123456789abcdef.json.gz');assert.ok(seen.every(s=>s[1]==='no-store'));
 for(const bad of [BASE+'../x',BASE+'server.mjs','https://evil.example/updates/latest.json'])assert.throws(()=>f(bad));
 assert.throws(()=>remoteFeed('http://insecure/',BASE));assert.throws(()=>remoteFeed('https://no-slash',BASE));
});
test('status handler reports the channel manifest, forwards other requests, and never uses production',async()=>{
 const root=await mkdtemp(join(tmpdir(),'chan-'));await writeFile(join(root,'test-channel.json'),JSON.stringify({base:CH}));
 const calls=[];let manifest={build:'a'.repeat(24),version:'0.2.test'};
 const h=createRemoteStatusHandler(root,async(req,opt)=>{calls.push([req.url,opt?.bypassCustomProtocolHandlers]);if(req.url.startsWith(CH))return manifest?Response.json(manifest):new Response('no',{status:404});return new Response('passthrough');});
 const ok=await(await h(new Request(CHECK_URL))).json();assert.deepEqual(ok,{build:'a'.repeat(24),version:'0.2.test'});
 assert.equal(await(await h(new Request('https://chip-arena-prototype.sheldon37.chatgpt.site/api/accounts'))).text(),'passthrough');
 manifest=null;const down=await h(new Request(CHECK_URL));assert.equal(down.status,503);
 assert.ok(calls.every(c=>c[1]===true));assert.ok(!calls.some(c=>c[0].startsWith(BASE)));
});
test('packager wires the remote channel files only when --channel is given',async()=>{
 const {execFileSync}=await import('node:child_process');const {ZipFile}=await import('node:zlib').then(()=>({ZipFile:null}));
 const dir=await mkdtemp(join(tmpdir(),'pkg-'));const py=`import zipfile,json,sys
d=sys.argv[1]
with zipfile.ZipFile(d+'/Chip-Arena-Desktop.zip','w') as z:
 p='Chip-Arena-Desktop/'
 z.writestr(p+'resources/app/package.json',json.dumps({'main':'main.cjs'}))
 z.writestr(p+'resources/game/launcher/manual-update.mjs','orig')
 z.writestr(p+'resources/game/launcher/update.mjs','U')
 z.writestr(p+'resources/game/release.json',json.dumps({'version':'x'}))`;
 execFileSync('python3',['-c',py,dir]);const {mkdir}=await import('node:fs/promises');await mkdir(join(dir,'feed'));await writeFile(join(dir,'feed','latest.json'),'{}');await mkdir(join(dir,'src','launcher'),{recursive:true});await writeFile(join(dir,'src','launcher','update.mjs'),'U');
 const pkg=new URL('./make-test-package.py',import.meta.url).pathname;
 const list=out=>execFileSync('python3',['-c','import zipfile,sys;z=zipfile.ZipFile(sys.argv[1]);print("\\n".join(z.namelist()));print(z.read("Chip-Arena-TEST-ONLY/resources/app/package.json").decode());print(z.read("Chip-Arena-TEST-ONLY/resources/game/launcher/manual-update.mjs").decode()[:200])',out]).toString();
 execFileSync('python3',[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'remote.zip'),join(dir,'src'),'--channel',CH]);
 execFileSync('python3',[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'local.zip'),join(dir,'src')]);
 const r=list(join(dir,'remote.zip')),l=list(join(dir,'local.zip'));
 assert.match(r,/test-channel\.json/);assert.match(r,/test-remote-bootstrap\.cjs"?/);assert.match(r,/remoteFeed/);assert.match(r,/test-update-status\.cjs/);
 assert.doesNotMatch(l,/test-channel\.json|remote-feed|remoteFeed/);assert.match(l,/"main": "test-bootstrap\.cjs"/);
 assert.throws(()=>execFileSync('python3',[pkg,join(dir,'Chip-Arena-Desktop.zip'),join(dir,'feed'),join(dir,'bad.zip'),join(dir,'src'),'--channel','http://x/'],{stdio:'pipe'}));
});
