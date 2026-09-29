// Test package replacement ONLY: UI still exercises the real updater and IPC.
import {applyUpdates,UPDATE_BASE} from './update.mjs';
import {localFeed} from './local-feed.mjs';
import {readFile} from 'node:fs/promises';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const send=x=>process.stdout.write(JSON.stringify(x)+'\n');
try{
 let running=false;try{running=(await fetch('http://127.0.0.1:'+Number(process.env.PORT||8788)+'/health',{signal:AbortSignal.timeout(500)})).ok;}catch{}
 if(running)throw Error('Close the separately running Chip Arena server before updating.');
 const installed=await applyUpdates(root,{force:true,strict:true,fetchImpl:localFeed(resolve(root,'test-feed'),UPDATE_BASE),log:text=>send({type:'progress',text})});
 const release=JSON.parse(await readFile(resolve(root,'release.json'),'utf8'));
 send({type:'result',status:installed?'installed':'current',version:release.version});
}catch(error){send({type:'error',text:error.message});process.exitCode=1;}
