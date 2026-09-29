// Existing runtime pins; downloads are explicit and are never a PR check.
import {readFile,mkdir,writeFile,rename,rm} from 'node:fs/promises';
import {createHash} from 'node:crypto';
const dir='.cache/runtimes';
const files=[
 ['node-v24.21.0-win-x64.zip','https://nodejs.org/dist/v24.21.0/node-v24.21.0-win-x64.zip','158f7685b44de51f6c0df1d153526cbcd3e1bc739a8dfc607721cef75de9e541'],
 ['electron-v44.4.5-win32-x64.zip','https://github.com/electron/electron/releases/download/v44.4.5/electron-v44.4.5-win32-x64.zip','11c395820a5aaa8ebcc0686b476d0ac98a730274ebfbdc8cf5538a7c2815cb5d'],
 ['cloudflared-windows-amd64.exe','https://github.com/cloudflare/cloudflared/releases/download/2026.9.3/cloudflared-windows-amd64.exe','f096265ec2fcbe9bb6e2d64268db167ced3fcbb83d894bdb9e2fcdb26f2ea7e2'],
];
const sha=b=>createHash('sha256').update(b).digest('hex');
await mkdir(dir,{recursive:true});
for(const [name,url,pin] of files){
 const path=dir+'/'+name;let cached;
 try{cached=await readFile(path);}catch(e){if(e.code!=='ENOENT')throw e;}
 if(cached){if(sha(cached)!==pin)throw Error(`Cached runtime checksum mismatch: ${name}`);console.log(`Verified ${name}`);continue;}
 const response=await fetch(url,{signal:AbortSignal.timeout(180000)});
 if(!response.ok)throw Error(`Runtime download failed: ${name} HTTP ${response.status}`);
 const bytes=Buffer.from(await response.arrayBuffer());
 if(sha(bytes)!==pin)throw Error(`Downloaded runtime checksum mismatch: ${name}`);
 const temporary=path+'.partial';
 try{await writeFile(temporary,bytes,{flag:'wx'});await rename(temporary,path);}finally{await rm(temporary,{force:true});}
 console.log(`Verified ${name}`);
}
