// Test package replacement ONLY: the real updater installs from the test channel in test-channel.json.
// Updater v2 ships this file inside schema-2 payloads as launcher/manual-update.mjs, so it must also run
// against a launcher/update.mjs without v2 (feature-detected below).
import * as installed from './update.mjs';
import {readFile} from 'node:fs/promises';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const send=x=>process.stdout.write(JSON.stringify(x)+'\n');
try{
 let running=false;try{running=(await fetch('http://127.0.0.1:'+Number(process.env.PORT||8788)+'/health',{signal:AbortSignal.timeout(500)})).ok;}catch{}
 if(running)throw Error('Close the separately running Chip Arena server before updating.');
 const channel=JSON.parse(await readFile(resolve(root,'test-channel.json'),'utf8'));
 const log=text=>send({type:'progress',text});
 // v2: apply a launcher update staged by the previous run before this run checks for updates.
 const updater=typeof installed.startLauncher==='function'?await installed.startLauncher(root,{log}):installed;
 const {remoteFeed}=await import('./remote-feed.mjs');
 const result=await updater.applyUpdates(root,{force:true,strict:true,schema2:channel.schema2===true,fetchImpl:remoteFeed(channel.base,updater.UPDATE_BASE),log});
 const release=JSON.parse(await readFile(resolve(root,'release.json'),'utf8'));
 send({type:'result',status:result?'installed':'current',version:release.version});
}catch(error){send({type:'error',text:error.message});process.exitCode=1;}
