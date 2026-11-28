import {spawn} from 'node:child_process';
import {mkdirSync,createWriteStream} from 'node:fs';
import {resolve} from 'node:path';
const mode=process.argv[2];
if(!['fast','full','standard','relay','relay-full'].includes(mode))throw Error('Use fast, standard, full, relay, or relay-full.');
// The relay serves the Unity client's accounts (/api/accounts/), room directory (/api/directory/) and rooms.
// CI runs only these; the frozen Electron game's own suites stay available locally (standard/full).
const relayTests=['tests/account-xp.test.ts','tests/accounts.test.ts','tests/directory.test.ts','tests/local-host-invite.test.ts','tests/party.test.ts','tests/relay.test.ts'];
mkdirSync('test-results',{recursive:true});
const npm=process.env.npm_execpath;
if(!npm)throw Error('Run this wrapper through npm.');
const runNpm=name=>[npm,'run',name];
const tasks=[['types',runNpm('check:types')]];
if(mode==='fast'){
 const tests=process.argv.slice(3);
 if(tests.some(t=>!/^tests\/[\w.-]+\.test\.ts$/.test(t)))throw Error('Pass explicit tests/*.test.ts paths.');
 if(tests.length)tasks.push(['affected-tests',['--import','tsx','--test',...tests]]);
}else if(mode.startsWith('relay')){
 tasks.push(['relay-unit',['--import','tsx','--test',...relayTests]],['relay-build',['scripts/build-relay.mjs']]);
 if(mode==='relay-full')tasks.push(['https',runNpm('test:https')]);
}else{
 tasks.push(['unit',runNpm('test')],['build',runNpm('build:check')]);
 if(mode==='full')tasks.push(['websocket',['tests/run-network.mjs']],['https',runNpm('test:https')]);
}
let failed=false;
for(const [name,args] of tasks){
 console.log(`\nChecking ${name}: node ${args.join(' ')}`);
 const log=createWriteStream(resolve('test-results',name+'.log'));
 const result=await new Promise(resolve=>{
  const child=spawn(process.execPath,args,{stdio:['ignore','pipe','pipe'],env:process.env});
  child.stdout.on('data',b=>{process.stdout.write(b);log.write(b);});
  child.stderr.on('data',b=>{process.stderr.write(b);log.write(b);});
  child.on('error',e=>{console.error(e.message);log.write(e.message);});
  child.on('close',(code,signal)=>resolve({code,signal}));
 });
 await new Promise(resolve=>log.end(resolve));
 if(result.code!==0){failed=true;console.error(`${name} FAILED (exit ${result.code}, signal ${result.signal}).`);}
}
process.exitCode=failed?1:0;
