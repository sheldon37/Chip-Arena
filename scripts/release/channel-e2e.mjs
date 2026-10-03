// Test tooling only (no publishing). End-to-end proof of the two-feed test channel with the REAL launcher code:
// an old TEST-ONLY install (pre-v2 ZIP) and a new one (candidate ZIP) update from a LOCAL HTTPS feed through their own
// desktop update.cjs (spawning launcher/manual-update.mjs exactly like the Update button) and badge handler.
// Usage (Node 24): node channel-e2e.mjs --candidate <candidate dir> --old-zip <pre-v2 TEST-ONLY.zip> --game <checkout> --work <empty dir>
import {spawn,execFileSync} from 'node:child_process';
import {createServer,get} from 'node:https';
import {createRequire} from 'node:module';
import {readFileSync,writeFileSync,mkdirSync,existsSync,readdirSync,rmSync,copyFileSync,statSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash,randomBytes} from 'node:crypto';
import {brotliDecompressSync,gunzipSync} from 'node:zlib';
import {holdSchema1,NOTICE} from './schema1-hold.mjs';
const arg=name=>{const i=process.argv.indexOf('--'+name);if(i<0||!process.argv[i+1])throw Error('Missing --'+name);return resolve(process.argv[i+1]);};
const candidate=arg('candidate'),oldZip=arg('old-zip'),game=arg('game'),work=arg('work');
if(Number(process.versions.node.split('.')[0])<24)throw Error('Run with Node 24 (real manifests require nodeMajor 24).');
const {encodeSchema1,encodeSchema2,manifest2,MiB}=await import(pathToFileURL(join(game,'scripts/update-format.mjs')).href);
const hash=b=>createHash('sha256').update(b).digest('hex'),require=createRequire(import.meta.url);
rmSync(work,{recursive:true,force:true});mkdirSync(work,{recursive:true});
const results=[];let failed=0;
function check(name,ok,details){results.push({name,pass:!!ok,...details});if(!ok)failed++;console.log((ok?'PASS ':'FAIL ')+name+(ok?'':' '+JSON.stringify(details)));}

// Local HTTPS channel. Children trust it through NODE_EXTRA_CA_CERTS; the badge forwarder passes the CA directly.
const key=join(work,'key.pem'),cert=join(work,'cert.pem');
execFileSync('openssl',['req','-x509','-newkey','rsa:2048','-nodes','-keyout',key,'-out',cert,'-days','1','-subj','/CN=127.0.0.1','-addext','subjectAltName=IP:127.0.0.1'],{stdio:'ignore'});
const ca=readFileSync(cert);process.env.NODE_EXTRA_CA_CERTS=cert;process.env.NO_PROXY=process.env.no_proxy='127.0.0.1,localhost';
let served='',requests=[],fault=null;
const server=createServer({key:readFileSync(key),cert:ca},(req,res)=>{
 const name=new URL(req.url,'https://local').pathname.replace(/^\/feed\//,'');requests.push(name);
 const file=join(served,name);if(!/^[\w.-]+$/.test(name)||!existsSync(file)){res.writeHead(404);res.end('missing');return;}
 const data=readFileSync(file);
 if(fault?.file===name&&fault.mode==='truncate'){res.writeHead(200,{'content-length':data.length});res.write(data.subarray(0,data.length>>1),()=>setTimeout(()=>res.destroy(),20));return;}
 res.writeHead(200,{'content-length':data.length,'content-type':'application/octet-stream'});res.end(data);
});
await new Promise(r=>server.listen(0,'127.0.0.1',r));const base=`https://127.0.0.1:${server.address().port}/feed/`;
const forward=request=>new Promise((ok,fail)=>get(request.url,{ca},res=>{const parts=[];res.on('data',c=>parts.push(c));res.on('end',()=>ok(new Response(Buffer.concat(parts),{status:res.statusCode})));}).on('error',fail));

// Installs: game root + Electron app scripts from a real TEST-ONLY ZIP (runtime binaries replaced by sentinels).
function install(zip,name){
 const dest=join(work,name);execFileSync('python3',['-c',`import zipfile,sys
z=zipfile.ZipFile(sys.argv[1]);p='Chip-Arena-TEST-ONLY/resources/'
for n in z.namelist():
 if (n.startswith(p+'game/') and not n.startswith(p+'game/runtime/')) or (n.startswith(p+'app/') and n.endswith('.cjs')):z.extract(n,sys.argv[2])`,zip,dest]);
 const resources=join(dest,'Chip-Arena-TEST-ONLY/resources'),root=join(resources,'game'),app=join(resources,'app');
 const channel=JSON.parse(readFileSync(join(root,'test-channel.json'),'utf8'));writeFileSync(join(root,'test-channel.json'),JSON.stringify({...channel,base}));
 // User data that no update may touch: TEST-PROFILE settings/session next to resources, runtime binaries, channel config.
 const profile=join(dest,'Chip-Arena-TEST-ONLY/TEST-PROFILE/Local Storage/leveldb');mkdirSync(profile,{recursive:true});writeFileSync(join(profile,'000003.log'),'chip-arena-preferences: saved settings');
 writeFileSync(join(dest,'Chip-Arena-TEST-ONLY/TEST-PROFILE/account-session.dat'),'saved session');mkdirSync(join(root,'runtime'),{recursive:true});writeFileSync(join(root,'runtime/node.exe'),'node runtime');writeFileSync(join(root,'runtime/cloudflared.exe'),'connector');
 const sentinels=[join(profile,'000003.log'),join(dest,'Chip-Arena-TEST-ONLY/TEST-PROFILE/account-session.dat'),join(root,'runtime/node.exe'),join(root,'runtime/cloudflared.exe'),join(root,'test-channel.json')];
 return {name,root,app,schema2:channel.schema2===true,sentinels,saved:sentinels.map(f=>hash(readFileSync(f)))};
}
const kept=i=>i.sentinels.every((f,n)=>existsSync(f)&&hash(readFileSync(f))===i.saved[n]);
const version=i=>JSON.parse(readFileSync(join(i.root,'release.json'),'utf8')).version;
const launcherHash=(i,path)=>existsSync(join(i.root,path))?hash(readFileSync(join(i.root,path))):null;
async function update(i){
 const {runUpdate,installedVersion}=require(join(i.app,'update.cjs')),progress=[],start=requests.length;
 const spawnImpl=(exe,args,options)=>spawn(process.execPath,args,options);
 try{const r=await runUpdate(i.root,{spawnImpl,onProgress:t=>progress.push(t),timeoutMs:120000});return {ok:true,...r,progress,requests:requests.slice(start),installed:await installedVersion(i.root)};}
 catch(e){return {ok:false,error:e.message,progress,requests:requests.slice(start),installed:await installedVersion(i.root)};}
}
async function badge(i){
 const {createRemoteStatusHandler}=require(join(i.app,'test-remote-status.cjs')),{CHECK_URL}=require(join(i.app,'test-update-status.cjs'));
 const start=requests.length,r=await createRemoteStatusHandler(i.root,forward)(new Request(CHECK_URL)),body=await r.json();
 const assets=join(i.root,'dist/client/assets'),current=existsSync(assets)&&readdirSync(assets).some(f=>f.endsWith('.js')&&readFileSync(join(assets,f),'utf8').includes(body.build));
 return {status:r.status,body,client:r.ok?current?'Latest build':'Update Available!':'Update check unavailable',requests:requests.slice(start)};
}
const feedFiles=dir=>readdirSync(dir).filter(f=>/^(latest(-v2)?\.json|arena-[a-f0-9]{16}\.(json\.gz|pack))$/.test(f));
function feed(name,files){const dir=join(work,'feeds',name);mkdirSync(dir,{recursive:true});for(const [file,data] of Object.entries(files))writeFileSync(join(dir,file),data);return dir;}

// Candidate content, decoded from the real schema-2 payload.
const cfeed=join(candidate,'feed'),cv1=JSON.parse(readFileSync(join(cfeed,'latest.json'))),cv2=JSON.parse(readFileSync(join(cfeed,'latest-v2.json')));
const body=brotliDecompressSync(readFileSync(join(cfeed,cv2.file))),indexLength=body.readUInt32BE(8),index=JSON.parse(body.subarray(12,12+indexLength));let at=12+indexLength;
const entries=index.files.map(f=>{const data=body.subarray(at,at+f.size);at+=f.size;return {path:f.path,data:Buffer.from(data)};});
const isLauncher=p=>p.startsWith('launcher/'),gameEntries=entries.filter(e=>!isLauncher(e.path)),launcherEntries=entries.filter(e=>isLauncher(e.path));
const release=JSON.parse(entries.find(e=>e.path==='release.json').data);
const sizes={gameBuild:cv1.build,version:cv1.version,schema1Bytes:cv1.bytes,schema2Bytes:cv2.bytes,saving:+(100*(1-cv2.bytes/cv1.bytes)).toFixed(1),unpackedBytes:cv2.unpackedBytes};
console.log('Sizes: '+JSON.stringify(sizes));
const withVersion=(list,v,extra=[])=>[...list.filter(e=>e.path!=='release.json'),...extra,{path:'release.json',data:Buffer.from(JSON.stringify({...release,version:v}))}];
function schema1(v,build,extra){const enc=encodeSchema1(withVersion(gameEntries,v,extra)),sha=hash(enc.gz),file='arena-'+sha.slice(0,16)+'.json.gz';return {[file]:enc.gz,'latest.json':JSON.stringify({schema:1,nodeMajor:24,build,version:v,file,sha256:sha,bytes:enc.gz.length})};}
function schema2(v,build,{extra=[],launcher=launcherEntries,manifest={}}={}){const all=[...withVersion(gameEntries,v,extra),...launcher],{pack,unpacked}=encodeSchema2(all,v,1),m={...manifest2({build,version:v,pack,unpacked,count:all.length,launcher}),...manifest};return {[m.file]:pack,'latest-v2.json':JSON.stringify(m)};}
const pad=n=>({path:'dist/client/music/e2e-pad.bin',data:randomBytes(n)});
const mark=(path,line)=>({path,data:Buffer.concat([launcherEntries.find(e=>e.path===path).data,Buffer.from('\n'+line+'\n')])});
const swap=(...changed)=>launcherEntries.map(e=>changed.find(c=>c.path===e.path)||e);

const oldInstall=install(oldZip,'old-install'),newInstall=install(join(candidate,'Chip-Arena-TEST-ONLY.zip'),'new-install');
check('old install is a pre-v2 launcher; new install is updater v2',!readFileSync(join(oldInstall.root,'launcher/update.mjs'),'utf8').includes('startLauncher')&&!oldInstall.schema2&&newInstall.schema2&&readFileSync(join(newInstall.root,'launcher/update.mjs'),'utf8').includes('startLauncher'),{old:oldInstall.schema2,new:newInstall.schema2});

// A. Old installs: schema 1 only, up to 32 MiB; refusal above; hold notice.
served=cfeed;
let b=await badge(oldInstall),u=await update(oldInstall);
check('A1 old badge reads only latest.json',b.status===200&&b.body.build===cv1.build&&b.requests.every(r=>r==='latest.json'),b);
check('A1 old launcher installs the candidate schema 1 and never reads v2 files',u.ok&&u.status==='installed'&&u.version===cv1.version&&u.requests.every(r=>r==='latest.json'||r===cv1.file)&&kept(oldInstall),u);
u=await update(oldInstall);check('A1 old launcher skips the same version',u.ok&&u.status==='current',u);
const big1=schema1('0.2.e2e31mib0000','e2e000000000000000000031',[pad(6*MiB)]),big1Bytes=Buffer.byteLength(Object.values(big1)[0]);
served=feed('v1-31mib',big1);u=await update(oldInstall);
check('A2 old launcher applies a schema 1 between the 25 MiB live guard and the 32 MiB cap',big1Bytes>25*MiB&&big1Bytes<=32*MiB&&u.ok&&u.status==='installed'&&version(oldInstall)==='0.2.e2e31mib0000'&&kept(oldInstall),{bytes:big1Bytes,...u});
const big2=schema1('0.2.e2e34mib0000','e2e000000000000000000034',[pad(9*MiB)]),big2Bytes=Buffer.byteLength(Object.values(big2)[0]);
served=feed('v1-34mib',big2);u=await update(oldInstall);
check('A3 old launcher refuses a schema 1 over 32 MiB and keeps its install',big2Bytes>32*MiB&&!u.ok&&/checksum/.test(u.error)&&version(oldInstall)==='0.2.e2e31mib0000'&&kept(oldInstall),{bytes:big2Bytes,...u});
// The packager never publishes that: schema 1 is held and old installs get the notice built from the last fitting payload.
const v2big=schema2('0.2.e2ebig400000','e2e0000000000000000000b2',{extra:[pad(40*MiB)],launcher:swap(mark('launcher/update.mjs','// e2e launcher v2'),mark('launcher/manual-update.mjs','// e2e entry v2'))});
const candidateOnly=feed('v2-big-candidate',v2big),holdDir=join(work,'feeds','hold');await holdSchema1(join(work,'feeds','v1-31mib'),candidateOnly,holdDir);
for(const f of feedFiles(candidateOnly))copyFileSync(join(candidateOnly,f),join(holdDir,f));
served=holdDir;b=await badge(oldInstall);u=await update(oldInstall);
check('A4 hold: old badge shows an update for the newer build',b.client==='Update Available!'&&b.body.build==='e2e0000000000000000000b2',b);
check('A4 hold: old Update says to download the new TEST ZIP and keeps the last fitting game',u.ok&&u.status==='installed'&&u.version==='0.2.e2e31mib0000'+NOTICE&&u.installed===u.version&&u.requests.every(r=>!r.includes('v2')&&!r.endsWith('.pack'))&&kept(oldInstall),u);
u=await update(oldInstall);check('A4 hold: repeat Update stays on the notice',u.ok&&u.status==='current'&&u.version.endsWith(NOTICE),u);

// B. New installs: v2 preferred, >32 MiB, launcher self-update, rollback, security.
served=cfeed;b=await badge(newInstall);u=await update(newInstall);
check('B1 new badge reads latest-v2.json',b.status===200&&b.requests[0]==='latest-v2.json'&&b.body.build===cv2.build,b);
check('B1 new launcher installs schema 2, not schema 1, with nothing to stage',u.ok&&u.status==='installed'&&u.version===cv2.version&&u.requests.includes(cv2.file)&&!u.requests.includes(cv1.file)&&!u.requests.includes('latest.json')&&!existsSync(join(newInstall.root,'runtime/launcher-pending'))&&kept(newInstall),u);
u=await update(newInstall);check('B1 new launcher skips the same version',u.ok&&u.status==='current'&&u.requests.length===1,u);
const before=launcherEntries.map(e=>[e.path,launcherHash(newInstall,e.path)]);
served=holdDir;b=await badge(newInstall);check('B2 new badge reads latest-v2.json while latest.json is the hold notice',b.client==='Update Available!'&&b.requests.length===1&&b.requests[0]==='latest-v2.json'&&b.body.build==='e2e0000000000000000000b2',b);
u=await update(newInstall);
check('B2 new launcher applies a schema 2 over 32 MiB (latest.json is the hold notice) and stages its launcher',u.ok&&u.status==='installed'&&u.version==='0.2.e2ebig400000'&&JSON.parse(v2big['latest-v2.json']).bytes>32*MiB&&u.progress.some(p=>/Downloading update/.test(p))&&existsSync(join(newInstall.root,'runtime/launcher-pending/pending.json'))&&before.every(([p,h])=>launcherHash(newInstall,p)===h)&&kept(newInstall),{bytes:JSON.parse(v2big['latest-v2.json']).bytes,...u});
u=await update(newInstall);
check('B2 next start activates the staged launcher files and reports current',u.ok&&u.status==='current'&&u.progress.includes('Launcher updated.')&&readFileSync(join(newInstall.root,'launcher/update.mjs'),'utf8').includes('// e2e launcher v2')&&readFileSync(join(newInstall.root,'launcher/manual-update.mjs'),'utf8').includes('// e2e entry v2')&&existsSync(join(newInstall.root,'runtime/launcher-probation.json')),u);
u=await update(newInstall);
check('B2 a later run with the new launcher confirms it and drops the backup',u.ok&&u.status==='current'&&!existsSync(join(newInstall.root,'runtime/launcher-probation.json'))&&!existsSync(join(newInstall.root,'runtime/launcher-backup'))&&kept(newInstall),u);
// B3. A new entry point that passes the syntax check but dies at start: the stable desktop code rolls it back.
const goodEntry=readFileSync(join(newInstall.root,'launcher/manual-update.mjs'));
served=feed('v2-broken-entry',schema2('0.2.e2ebroken000','e2e0000000000000000000b3',{launcher:swap(mark('launcher/update.mjs','// e2e launcher v2'),{path:'launcher/manual-update.mjs',data:Buffer.from("throw new Error('e2e broken entry');\n")})}));
const staged=await update(newInstall),activated=await update(newInstall),broken=await update(newInstall);
check('B3 a broken entry point is rolled back by desktop update.cjs',staged.ok&&staged.status==='installed'&&activated.ok&&activated.progress.includes('Launcher updated.')&&!broken.ok&&/previous launcher was restored/.test(broken.error)&&hash(readFileSync(join(newInstall.root,'launcher/manual-update.mjs')))===hash(goodEntry)&&!existsSync(join(newInstall.root,'runtime/launcher-probation.json'))&&kept(newInstall),{staged,activated,broken});
const pending=join(newInstall.root,'runtime/launcher-pending'),fixed=(v,build)=>schema2(v,build,{launcher:swap(mark('launcher/update.mjs','// e2e launcher v2'),{path:'launcher/manual-update.mjs',data:goodEntry})});
served=feed('v2-fixed',fixed('0.2.e2efixed0000','e2e0000000000000000000f1'));const recovered=await update(newInstall);
check('B3 a fixed publish installs with the restored launcher and nothing staged',recovered.ok&&recovered.status==='installed'&&!existsSync(pending)&&kept(newInstall),recovered);
// B4. A new update.mjs that does not provide the launcher API: rolled back in-process, update continues.
const goodUpdate=readFileSync(join(newInstall.root,'launcher/update.mjs'));
served=feed('v2-bad-api',schema2('0.2.e2ebadapi000','e2e0000000000000000000b4',{launcher:swap({path:'launcher/update.mjs',data:Buffer.from('export const LAUNCHER_API=1;\n')},{path:'launcher/manual-update.mjs',data:goodEntry})}));
const s4=await update(newInstall),r4=await update(newInstall);
check('B4 an incompatible update.mjs is rolled back and the running launcher completes the update',s4.ok&&s4.status==='installed'&&r4.ok&&r4.progress.some(p=>/previous launcher was restored/.test(p))&&hash(readFileSync(join(newInstall.root,'launcher/update.mjs')))===hash(goodUpdate)&&kept(newInstall),{s4,r4});
// The feed still lists that file, so it was staged again; a fixed publish clears it.
served=feed('v2-fixed2',fixed('0.2.e2efixed0001','e2e0000000000000000000f2'));const settle=await update(newInstall);
check('B4 a fixed publish installs and leaves nothing staged',settle.ok&&settle.status==='installed'&&!existsSync(pending)&&hash(readFileSync(join(newInstall.root,'launcher/update.mjs')))===hash(goodUpdate),settle);
// B5. Security over the real network path; every refusal leaves the install as it was.
const steady=version(newInstall),leftovers=()=>['runtime/update-download.part','runtime/update-stage','runtime/launcher-next'].filter(p=>existsSync(join(newInstall.root,p)));
const attack=async(name,files,expect,setup)=>{served=feed(name,files);fault=setup?.(files)||null;const r=await update(newInstall);fault=null;check('B5 '+name,!r.ok&&expect.test(r.error)&&version(newInstall)===steady&&!leftovers().length&&kept(newInstall)&&!existsSync(join(newInstall.root,'..','evil.txt')),r);return r;};
const evil=(()=>{const all=[...withVersion(gameEntries,'0.2.e2eevil00000'),{path:'../evil.txt',data:Buffer.from('x')}],idx=Buffer.from(JSON.stringify({schema:2,version:'0.2.e2eevil00000',files:all.map(e=>({path:e.path,size:e.data.length,sha256:hash(e.data)}))}));const head=Buffer.alloc(12);head.write('CHIPPACK',0,'latin1');head.writeUInt32BE(idx.length,8);const {brotliCompressSync}=require('node:zlib');const pack=brotliCompressSync(Buffer.concat([head,idx,...all.map(e=>e.data)]));const sha=hash(pack);const m={schema:2,encoding:'br',nodeMajor:24,build:'e2e0000000000000000000e1',version:'0.2.e2eevil00000',file:'arena-'+sha.slice(0,16)+'.pack',sha256:sha,bytes:pack.length,unpackedBytes:all.reduce((n,e)=>n+e.data.length,0),files:all.length,launcher:[]};return {[m.file]:pack,'latest-v2.json':JSON.stringify(m)};})();
await attack('path traversal in the container',evil,/Invalid update path/);
const flip=files=>{const out={...files};for(const [n,d] of Object.entries(out))if(n.endsWith('.pack')){const c=Buffer.from(d);c[c.length>>1]^=0xff;out[n]=c;}return out;};
const normal=schema2('0.2.e2eattack000','e2e0000000000000000000e2');
await attack('hash mismatch',flip(normal),/checksum mismatch/);
await attack('body larger than its manifest',Object.fromEntries(Object.entries(normal).map(([n,d])=>[n,n.endsWith('.pack')?Buffer.concat([d,Buffer.from('extra')]):d])),/larger than its manifest/);
await attack('truncated download (connection dropped)',normal,/Update download failed|incomplete/,files=>({file:Object.keys(files).find(n=>n.endsWith('.pack')),mode:'truncate'}));
const huge=JSON.parse(normal['latest-v2.json']);const r5=await attack('oversize manifest (over 256 MiB) is refused before downloading',{'latest-v2.json':JSON.stringify({...huge,bytes:300*MiB})},/larger than this launcher accepts \(256 MiB\)/);
check('B5 oversize refusal requested no payload',r5.requests.length===1&&r5.requests[0]==='latest-v2.json',r5);
served=cfeed;const back=await update(newInstall);check('B6 the new install still updates normally after every refusal',back.ok&&back.status==='installed'&&back.version===cv2.version&&kept(newInstall),back);

server.close();
const report={sizes,passed:results.filter(r=>r.pass).length,failed,results:results.map(({name,pass})=>({name,pass}))};
writeFileSync(join(work,'e2e-report.json'),JSON.stringify({...report,details:results},null,1));
console.log(JSON.stringify(report,null,1));process.exitCode=failed?1:0;
