import {spawn} from 'node:child_process';
import {resolve} from 'node:path';
import {cpSync,rmSync,mkdtempSync} from 'node:fs';
import {tmpdir} from 'node:os';
const source=process.env.CHIP_BUNDLE_DIR;if(!source)throw Error('Set CHIP_BUNDLE_DIR to the extracted portable folder.');
const temp=mkdtempSync(resolve(tmpdir(),'chip portable test ')),folder=temp+'/Game With Spaces';
cpSync(source,folder,{recursive:true});
const server=spawn(process.execPath,[folder+'/launcher/start.mjs'],{stdio:'inherit',env:{...process.env,PORT:'8797',CHIP_NO_BROWSER:'1'}});
const pause=ms=>new Promise(r=>setTimeout(r,ms));
try{
 let ready=false;for(let n=0;n<80;n++){try{ready=(await(await fetch('http://127.0.0.1:8797/health')).json()).application==='chip-arena';}catch{}if(ready)break;await pause(100);}if(!ready)throw Error('Portable launcher did not become ready.');
 const test=spawn(process.execPath,['--import','tsx','tests/portable-smoke.ts'],{stdio:'inherit'});const code=await new Promise(r=>test.on('exit',r));if(code)process.exitCode=code;
}finally{server.kill('SIGTERM');await new Promise(r=>server.once('exit',r));await pause(650);let stopped=false;try{await fetch('http://127.0.0.1:8797/health');}catch{stopped=true;}if(!stopped){process.exitCode=1;console.error('Launcher left the server running.');}else console.log('Launcher shutdown: server stopped.');rmSync(temp,{recursive:true,force:true});}
