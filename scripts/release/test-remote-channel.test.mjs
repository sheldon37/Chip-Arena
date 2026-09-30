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
