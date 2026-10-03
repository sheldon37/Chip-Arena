// Test tooling only. When a build's schema-1 payload no longer fits the 32 MiB cap of launchers installed before
// updater v2, latest.json must stay on the last version that fit. This turns that last payload into a notice for
// those old installs: identical game files, but release.json's version tells them to download the new TEST-ONLY ZIP,
// and the manifest's build identity changes so their in-game badge shows that an update exists. The manifest also says
// hold:true: launchers older than v2 ignore unknown fields, and v2 launchers refuse it instead of downgrading.
// Usage: node schema1-hold.mjs <current channel feed dir> <candidate feed dir> <output dir>
import {readFile,writeFile,mkdir,readdir} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
import {gzipSync,gunzipSync} from 'node:zlib';
export const NOTICE='-download-the-new-TEST-ZIP-from-GitHub-Releases';
const MiB=1024*1024,hash=b=>createHash('sha256').update(b).digest('hex');
export async function holdSchema1(channelFeed,candidateFeed,output){
 const names=await readdir(candidateFeed);
 if(names.includes('latest.json'))throw Error('The candidate still has a schema-1 latest.json; publish it normally.');
 const next=JSON.parse(await readFile(join(candidateFeed,'latest-v2.json'),'utf8'));
 if(next.schema!==2||!/^[a-f0-9]{24}$/.test(next.build))throw Error('Candidate latest-v2.json is not a schema-2 manifest');
 const current=JSON.parse(await readFile(join(channelFeed,'latest.json'),'utf8'));
 if(current.schema!==1||!/^arena-[a-f0-9]{16}\.json\.gz$/.test(current.file))throw Error('Channel latest.json is not a schema-1 manifest');
 const bytes=await readFile(join(channelFeed,current.file));if(hash(bytes)!==current.sha256)throw Error('Channel payload does not match latest.json');
 await mkdir(output,{recursive:true});
 if(current.version.endsWith(NOTICE)){
  // Already a notice: keep its payload and only point the badge at the newest build.
  const manifest={...current,build:next.build,hold:true};await writeFile(join(output,'latest.json'),JSON.stringify(manifest));await writeFile(join(output,current.file),bytes);
  return {manifest,reused:true};
 }
 const version=current.version+NOTICE;if(!/^[\w.-]{1,80}$/.test(version))throw Error('Notice version is too long: '+version);
 const payload=JSON.parse(gunzipSync(bytes));const entry=payload.files.find(f=>f.path==='release.json');
 const release=JSON.parse(Buffer.from(entry.data,'base64'));if(release.version!==current.version)throw Error('Channel payload release.json does not match latest.json');
 const data=Buffer.from(JSON.stringify({...release,version}));entry.data=data.toString('base64');entry.sha256=hash(data);
 const json=JSON.stringify(payload),gz=gzipSync(json,{level:9}),sha256=hash(gz),file='arena-'+sha256.slice(0,16)+'.json.gz';
 // The same limits every pre-v2 launcher enforces.
 const unpacked=payload.files.reduce((n,f)=>n+Buffer.from(f.data,'base64').length,0);
 if(gz.length>32*MiB||Buffer.byteLength(json)>96*MiB||unpacked>64*MiB||payload.files.length>1500)throw Error('Notice payload exceeds the installed-launcher limits');
 const manifest={schema:1,nodeMajor:24,build:next.build,version,file,sha256,bytes:gz.length,hold:true};
 await writeFile(join(output,file),gz);await writeFile(join(output,'latest.json'),JSON.stringify(manifest));
 return {manifest,reused:false};
}
if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
 const [channelFeed,candidateFeed,output]=process.argv.slice(2);
 if(!output){console.error('Usage: node schema1-hold.mjs <current channel feed dir> <candidate feed dir> <output dir>');process.exit(2);}
 const {manifest,reused}=await holdSchema1(channelFeed,candidateFeed,output);console.log(JSON.stringify({held:true,reused,...manifest}));
}
