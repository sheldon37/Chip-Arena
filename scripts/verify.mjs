import {spawn} from 'node:child_process';
import {mkdirSync,createWriteStream} from 'node:fs';
import {resolve} from 'node:path';
const mode=process.argv[2];
if(!['fast','full','standard'].includes(mode))throw Error('Use fast, standard, or full.');
mkdirSync('test-results',{recursive:true});
const npm=process.env.npm_execpath;
if(!npm)throw Error('Run this wrapper through npm.');
const runNpm=name=>[npm,'run',name];
const tasks=[['types',runNpm('check:types')]];
if(mode==='fast'){
 const tests=process.argv.slice(3);
 if(tests.some(t=>!/^tests\/[\w.-]+\.test\.ts$/.test(t)))throw Error('Pass explicit tests/*.test.ts paths.');
 if(tests.length)tasks.push(['affected-tests',['--import','tsx','--test',...tests]]);
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
