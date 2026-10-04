// Update payload formats and packaging limits. The launcher (launcher/update.mjs) is the decoder and the
// single source of truth for installed-updater limits, the launcher whitelist and the schema-2 magic.
import {createHash} from 'node:crypto';
import {gzipSync,brotliCompressSync,constants} from 'node:zlib';
import {readFileSync,readdirSync,statSync} from 'node:fs';
import {join} from 'node:path';
import {LIMITS,LAUNCHER_FILES,PACK_MAGIC} from '../launcher/update.mjs';
export const MiB=1024*1024;
/** The live Site (Cloudflare Pages) serves at most 25 MiB per file. */
export const LIVE_HOSTING_MAX=25*MiB;
/** raw.githubusercontent.com serves repository files; GitHub rejects files over 100 MB (decimal). */
export const GITHUB_FILE_MAX=100*1000*1000,GITHUB_FILE_WARN=50*1000*1000;
export const TEST_SCHEMA2_MAX=95*MiB;
export const hash=b=>createHash('sha256').update(b).digest('hex');
const mib=(env,name,fallback,max)=>{const value=env[name];if(value===undefined||value==='')return fallback;const n=Number(value);if(!Number.isFinite(n)||n<1||n*MiB>max)throw Error(name+' must be a number of MiB from 1 to '+max/MiB);return Math.floor(n*MiB);};
// Brotli quality 11 is the smallest output (about 85 s for the current build); lower values exist for quick local tests.
const brotliQuality=value=>{if(value===undefined||value==='')return 11;const q=Number(value);if(!Number.isInteger(q)||q<1||q>11)throw Error('CHIP_UPDATE_SCHEMA2_QUALITY must be an integer from 1 to 11');return q;};
/**
 * live (default): schema 1 only, 25 MiB guard, overflow fails the build. Unchanged from before updater v2.
 * In live, CHIP_UPDATE_SCHEMA1_MAX_MIB can only lower 25 MiB (the Site serves no larger file), and schema 2 is refused:
 * live launchers never read it, and launcher self-update is for the test channel only.
 * test-channel (set by candidate.py --channel): schema 1 up to the installed-updater cap (32 MiB) for old
 * installs, plus schema 2 (latest-v2.json) for v2 launchers; a schema 1 that no longer fits is held back.
 */
export function packagerConfig(env=process.env){
 const profile=env.CHIP_UPDATE_PROFILE||'live';if(!['live','test-channel'].includes(profile))throw Error('CHIP_UPDATE_PROFILE must be live or test-channel');
 const test=profile==='test-channel',set=name=>env[name]!==undefined&&env[name]!=='';
 if(!test&&['CHIP_UPDATE_SCHEMA2','CHIP_UPDATE_SCHEMA2_MAX_MIB','CHIP_UPDATE_LAUNCHER_OVERLAY'].some(set))throw Error('Schema 2 and launcher overlays are test-channel only (CHIP_UPDATE_PROFILE=test-channel); live builds produce schema 1.');
 if(test&&set('CHIP_UPDATE_SCHEMA2')&&env.CHIP_UPDATE_SCHEMA2!=='1')throw Error('The test-channel profile always produces schema 2.');
 return {profile,schema1Max:mib(env,'CHIP_UPDATE_SCHEMA1_MAX_MIB',test?LIMITS.schema1Bytes:LIVE_HOSTING_MAX,test?LIMITS.schema1Bytes:LIVE_HOSTING_MAX),schema1Overflow:test?'hold':'fail',
  schema2:test,schema2Max:test?mib(env,'CHIP_UPDATE_SCHEMA2_MAX_MIB',TEST_SCHEMA2_MAX,LIMITS.schema2Bytes):0,
  quality:brotliQuality(env.CHIP_UPDATE_SCHEMA2_QUALITY),overlay:env.CHIP_UPDATE_LAUNCHER_OVERLAY||''};
}
/** Schema 1: gzip(JSON {schema:1, files:[{path, sha256, data:base64}]}), byte-identical to the original packager. */
export function encodeSchema1(entries){
 const files=entries.map(f=>({path:f.path,sha256:hash(f.data),data:f.data.toString('base64')})),json=JSON.stringify({schema:1,files});
 return {gz:gzipSync(json,{level:9}),jsonBytes:Buffer.byteLength(json),unpacked:entries.reduce((n,f)=>n+f.data.length,0),count:entries.length};
}
/** Why every launcher installed so far would refuse this schema-1 payload, or the hosting limit it breaks. */
export function schema1Problem(encoded,max){
 if(encoded.gz.length>max)return 'payload '+encoded.gz.length+' bytes exceeds the '+(max/MiB).toFixed(0)+' MiB schema-1 limit';
 if(encoded.jsonBytes>LIMITS.schema1Json)return 'expanded JSON exceeds the installed updater limit';
 if(encoded.unpacked>LIMITS.schema1Unpacked)return 'unpacked files exceed the installed updater limit';
 if(encoded.count>LIMITS.schema1Files)return 'too many files for the installed updater';
 return null;
}
/** Schema 2: Brotli of [PACK_MAGIC, uint32 BE index length, JSON index, file bytes in index order]. No base64. */
export function encodeSchema2(entries,version,quality=11){
 const index=Buffer.from(JSON.stringify({schema:2,version,files:entries.map(f=>({path:f.path,size:f.data.length,sha256:hash(f.data)}))}));
 const head=Buffer.alloc(12);head.write(PACK_MAGIC,0,'latin1');head.writeUInt32BE(index.length,8);
 const body=Buffer.concat([head,index,...entries.map(f=>f.data)]);
 return {pack:brotliCompressSync(body,{params:{[constants.BROTLI_PARAM_QUALITY]:quality,[constants.BROTLI_PARAM_LGWIN]:24,[constants.BROTLI_PARAM_SIZE_HINT]:body.length}}),unpacked:entries.reduce((n,f)=>n+f.data.length,0)};
}
/**
 * Launcher files carried by schema 2: the repository's update.mjs/start.mjs/manual-update.mjs, replaced or extended
 * by an overlay directory. A test-channel payload must carry the channel's own manual-update.mjs, never the
 * production one that reads the live feed.
 */
export function launcherEntries(config,repo='.'){
 const files=new Map();for(const name of ['update.mjs','start.mjs','manual-update.mjs'])files.set('launcher/'+name,readFileSync(join(repo,'launcher',name)));
 const overlaid=new Set();
 if(config.overlay)for(const name of readdirSync(config.overlay).sort()){const path='launcher/'+name;if(!LAUNCHER_FILES.includes(path)||!statSync(join(config.overlay,name)).isFile())throw Error('Launcher overlay file is not on the launcher whitelist: '+name);files.set(path,readFileSync(join(config.overlay,name)));overlaid.add(path);}
 if(config.profile==='test-channel'&&!overlaid.has('launcher/manual-update.mjs'))throw Error('A test-channel payload must carry the test-channel launcher/manual-update.mjs (set CHIP_UPDATE_LAUNCHER_OVERLAY).');
 return [...files].sort(([a],[b])=>a<b?-1:a>b?1:0).map(([path,data])=>({path,data}));
}
export function manifest2({build,version,pack,unpacked,count,launcher}){
 const sha256=hash(pack);
 return {schema:2,encoding:'br',nodeMajor:24,build,version,file:'arena-'+sha256.slice(0,16)+'.pack',sha256,bytes:pack.length,unpackedBytes:unpacked,files:count,launcher:launcher.map(f=>({path:f.path,sha256:hash(f.data)}))};
}
