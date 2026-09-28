import {build} from 'esbuild';
import {readFileSync,writeFileSync,readdirSync,statSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {gzipSync} from 'node:zlib';
const hash=b=>createHash('sha256').update(b).digest('hex');
await build({entryPoints:['src/server/index.ts'],outfile:'/tmp/chip-arena-update-server.mjs',bundle:true,platform:'node',format:'esm',target:'node24',banner:{js:"import {createRequire} from 'node:module';const require=createRequire(import.meta.url);"}});
const files=[];const add=(path,source=path)=>{const b=readFileSync(source);files.push({path,sha256:hash(b),data:b.toString('base64')});};
// Editable source is distributed separately; embedding it duplicates every runtime asset.
const walk=path=>{for(const name of readdirSync(path)){if(name==='updates'||name==='chip-arena-source.zip')continue;const p=path+'/'+name;if(statSync(p).isDirectory())walk(p);else add(p);}};
add('server.mjs','/tmp/chip-arena-update-server.mjs');walk('dist/client');walk('drizzle');
const version='0.2.'+hash(JSON.stringify(files.map(f=>[f.path,f.sha256]))).slice(0,12);const release=Buffer.from(JSON.stringify({version,channel:'stable',schema:1,nodeMajor:24}));files.push({path:'release.json',sha256:hash(release),data:release.toString('base64')});
const bytes=gzipSync(JSON.stringify({schema:1,files}),{level:9}),sha256=hash(bytes),file='arena-'+sha256.slice(0,16)+'.json.gz';if(bytes.length>25*1024*1024)throw Error('Automatic update exceeds the hosting 25 MiB per-file limit.');mkdirSync('dist/client/updates',{recursive:true});writeFileSync('dist/client/updates/'+file,bytes);writeFileSync('dist/client/updates/latest.json',JSON.stringify({schema:1,nodeMajor:24,version,file,sha256,bytes:bytes.length}));writeFileSync('/tmp/chip-arena-release.json',release);console.log(JSON.stringify({updateVersion:version,bytes:bytes.length,files:files.length}));
