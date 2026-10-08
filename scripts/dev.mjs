import {spawn} from 'node:child_process';
import {createWriteStream,mkdirSync} from 'node:fs';
mkdirSync('.dev-logs',{recursive:true});const log=createWriteStream('.dev-logs/server.log',{flags:'a'});
const server=spawn(process.execPath,['--import','tsx','src/server/index.ts'],{stdio:['ignore','pipe','pipe'],env:{...process.env,PORT:'8788',ALLOW_TEST_ROOMS:'true'}});
server.stdout.on('data',b=>{process.stdout.write(b);log.write(b);});server.stderr.on('data',b=>{process.stderr.write(b);log.write(b);});server.on('exit',(code,signal)=>log.write(`Backend exit ${code} ${signal}\n`));
const client=spawn(process.execPath,['node_modules/vite/bin/vite.js','--host','0.0.0.0','--port','4173',...process.argv.slice(2)],{stdio:'inherit',env:process.env});
function close(){server.kill();client.kill();}process.on('SIGINT',close);process.on('SIGTERM',close);client.on('exit',code=>{server.kill();process.exit(code||0);});
