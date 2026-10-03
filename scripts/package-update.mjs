import {build} from 'esbuild';
import {readFileSync,writeFileSync,readdirSync,statSync,mkdirSync} from 'node:fs';
import {packagerConfig,encodeSchema1,schema1Problem,encodeSchema2,launcherEntries,manifest2,hash,MiB,GITHUB_FILE_MAX,GITHUB_FILE_WARN} from './update-format.mjs';
// Profiles and limits: scripts/update-format.mjs and docs/DEVELOPMENT.md ("Update payload limits").
const config=packagerConfig(process.env);
mkdirSync('.cache/release',{recursive:true});
const buildId=readFileSync('src/client/build-info.generated.ts','utf8').match(/GAME_BUILD='([a-f0-9]{24})'/)?.[1];if(!buildId)throw Error('Missing build identity');
await build({entryPoints:['src/server/index.ts'],outfile:'.cache/release/server.mjs',bundle:true,platform:'node',format:'esm',target:'node24',banner:{js:"import {createRequire} from 'node:module';const require=createRequire(import.meta.url);"}});
const entries=[];const add=(path,source=path)=>{entries.push({path,data:readFileSync(source)});};
// Editable source is distributed separately; embedding it duplicates every runtime asset.
const walk=path=>{for(const name of readdirSync(path)){if(name==='updates'||name==='chip-arena-source.zip')continue;const p=path+'/'+name;if(statSync(p).isDirectory())walk(p);else add(p);}};
add('server.mjs','.cache/release/server.mjs');walk('dist/client');walk('drizzle');
// The content version covers game files only, exactly as before, so schema 1 and schema 2 of one build share it.
const version='0.2.'+hash(JSON.stringify(entries.map(f=>[f.path,hash(f.data)]))).slice(0,12);const release=Buffer.from(JSON.stringify({version,channel:'stable',schema:1,nodeMajor:24}));entries.push({path:'release.json',data:release});
const v1=encodeSchema1(entries),problem=schema1Problem(v1,config.schema1Max);
if(problem&&config.schema1Overflow==='fail')throw Error('Automatic update exceeds the '+(config.schema1Max/MiB).toFixed(0)+' MiB '+config.profile+' schema-1 limit ('+problem+').');
const report={updateVersion:version,bytes:problem?null:v1.gz.length,files:entries.length,profile:config.profile,build:buildId};
mkdirSync('dist/client/updates',{recursive:true});
if(problem){
 // Old launchers keep the last schema-1 version that fit; the channel tooling turns latest.json into a notice.
 report.schema1={held:true,bytes:v1.gz.length,limit:config.schema1Max,reason:problem};console.warn('Schema 1 held back: '+problem+'. Only latest-v2.json is produced.');
}else{
 const sha256=hash(v1.gz),file='arena-'+sha256.slice(0,16)+'.json.gz';writeFileSync('dist/client/updates/'+file,v1.gz);writeFileSync('dist/client/updates/latest.json',JSON.stringify({schema:1,nodeMajor:24,build:buildId,version,file,sha256,bytes:v1.gz.length}));
 report.schema1={file,bytes:v1.gz.length,limit:config.schema1Max};
}
if(config.schema2){
 const launcher=launcherEntries(config),all=[...entries,...launcher],{pack,unpacked}=encodeSchema2(all,version,config.quality);
 if(pack.length>config.schema2Max)throw Error('Schema-2 update is '+pack.length+' bytes, over the '+(config.schema2Max/MiB).toFixed(0)+' MiB '+config.profile+' limit.');
 if(pack.length>GITHUB_FILE_MAX)throw Error('Schema-2 update exceeds the GitHub 100 MB file limit.');if(pack.length>GITHUB_FILE_WARN)console.warn('Schema-2 update is over 50 MB; GitHub warns about files this large.');
 const manifest=manifest2({build:buildId,version,pack,unpacked,count:all.length,launcher});writeFileSync('dist/client/updates/'+manifest.file,pack);writeFileSync('dist/client/updates/latest-v2.json',JSON.stringify(manifest));
 report.schema2={file:manifest.file,bytes:pack.length,unpackedBytes:unpacked,limit:config.schema2Max,quality:config.quality,launcher:manifest.launcher.map(f=>f.path)};
}
writeFileSync('.cache/release/release.json',release);writeFileSync('.cache/release/update-report.json',JSON.stringify(report,null,1)+'\n');
console.log(JSON.stringify(report));
