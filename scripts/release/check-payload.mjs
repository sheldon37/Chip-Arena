import assert from 'node:assert/strict';
import {readFile,mkdtemp,mkdir,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {gunzipSync} from 'node:zlib';
import {pathToFileURL} from 'node:url';
const {applyUpdates,UPDATE_BASE,allowedPath}=await import(pathToFileURL(resolve(process.argv[3],'launcher/update.mjs')).href);
import {localFeed} from './local-feed.mjs';
const feed=resolve(process.argv[2]);
const manifest=JSON.parse(await readFile(join(feed,'latest.json')));
assert.match(manifest.file,/^arena-[a-f0-9]{16}\.json\.gz$/);
const bytes=await readFile(join(feed,manifest.file));
const hash=b=>createHash('sha256').update(b).digest('hex');
assert.equal(hash(bytes),manifest.sha256);assert.equal(bytes.length,manifest.bytes);
const payload=JSON.parse(gunzipSync(bytes));
assert(payload.files.every(f=>allowedPath(f.path)));
const root=await mkdtemp(join(tmpdir(),'chip-candidate-'));
try{
 await mkdir(join(root,'dist/client'),{recursive:true});await mkdir(join(root,'drizzle'));
 await writeFile(join(root,'server.mjs'),'old');await writeFile(join(root,'dist/client/index.html'),'old');
 await writeFile(join(root,'release.json'),JSON.stringify({version:'test-seed'}));
 await writeFile(join(root,'settings-sentinel'),'untouched');
 const fetchImpl=localFeed(feed,UPDATE_BASE);
 await assert.rejects(fetchImpl('https://unexpected.example/latest.json'));
 assert.equal(await applyUpdates(root,{fetchImpl,force:true,strict:true,log:()=>{}}),true);
 for(const f of payload.files){assert.equal(hash(await readFile(join(root,f.path))),f.sha256,f.path);}
 assert.equal(await readFile(join(root,'settings-sentinel'),'utf8'),'untouched');
 assert.equal(await applyUpdates(root,{fetchImpl,force:true,strict:true,log:()=>{}}),false);
 await assert.rejects(applyUpdates(root,{force:true,strict:true,log:()=>{},fetchImpl:async()=>{throw Error('offline');}}),/offline/);
 const corrupt=async url=>String(url).endsWith('latest.json')?Response.json({...manifest,version:'bad',sha256:'0'.repeat(64)}):new Response(bytes);
 await assert.rejects(applyUpdates(root,{fetchImpl:corrupt,force:true,strict:true,log:()=>{}}),/checksum/);
 assert.equal(JSON.parse(await readFile(join(root,'release.json'))).version,manifest.version);
 console.log(JSON.stringify({result:'passed',version:manifest.version,filesVerified:payload.files.length,checks:['apply actual payload','verify every installed file','same-version skip','offline refusal','corrupt hash refusal','settings sentinel','test feed URL restriction']}));
}finally{await rm(root,{recursive:true,force:true});}
