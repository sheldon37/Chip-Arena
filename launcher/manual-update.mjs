import {startLauncher} from './update.mjs';
import {readFile} from 'node:fs/promises';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const send=message=>process.stdout.write(JSON.stringify(message)+'\n');
try{
 // The launcher stops its owned server first; also refuse a separately started .bat server.
 let running=false;try{running=(await fetch('http://127.0.0.1:'+Number(process.env.PORT||8788)+'/health',{signal:AbortSignal.timeout(500)})).ok;}catch{}
 if(running)throw Error('Close the separately running Chip Arena server before updating.');
 // A launcher update staged by the previous run is applied here, before this run checks for updates.
 const log=text=>send({type:'progress',text}),updater=await startLauncher(root,{log});
 const installed=await updater.applyUpdates(root,{force:true,strict:true,log});
 const release=JSON.parse(await readFile(resolve(root,'release.json'),'utf8'));
 send({type:'result',status:installed?'installed':'current',version:release.version});
}catch(error){send({type:'error',text:error.message});process.exitCode=1;}
