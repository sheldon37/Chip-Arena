import {startLauncher} from './update.mjs';
import {spawn} from 'node:child_process';
import {networkInterfaces} from 'node:os';
import {readFile,writeFile,mkdir,rename} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

const root=fileURLToPath(new URL('../',import.meta.url));
process.chdir(root);
const port=Number(process.env.PORT||8788),online=process.argv.includes('--online');
if(!Number.isInteger(port)||port<1024||port>65535)throw new Error('PORT must be between 1024 and 65535.');
const local=`http://127.0.0.1:${port}`,children=new Set();let stopping=false,gameServer;
function start(file,args,stdio='inherit'){const child=spawn(file,args,{cwd:root,stdio,windowsHide:true,env:{...process.env,PORT:String(port),ALLOW_TEST_ROOMS:'false'}});children.add(child);child.on('error',e=>{console.error(e.message);stop(1);});child.on('exit',()=>{children.delete(child);if(!stopping){console.error('A server process stopped. Restart the launcher to play again.');stop(1);}});return child;}
function stop(code=0){if(stopping)return;stopping=true;for(const child of children)child.kill();process.exitCode=code;setTimeout(()=>process.exit(code),500).unref();}
process.on('SIGINT',()=>stop());process.on('SIGTERM',()=>stop());
function open(url){if(process.env.CHIP_NO_BROWSER==='1')return;const command=process.platform==='win32'?'rundll32.exe':process.platform==='darwin'?'open':'xdg-open';const args=process.platform==='win32'?['url.dll,FileProtocolHandler',url]:[url];const child=spawn(command,args,{detached:true,stdio:'ignore',windowsHide:true});child.on('error',()=>console.log('Open this link in your browser:',url));child.unref();}
async function health(){try{const r=await fetch(local+'/health',{signal:AbortSignal.timeout(600)});return r.ok?await r.json():null;}catch{return null;}}
async function tunnel(){
 if(process.platform!=='win32')throw new Error('The optional online launcher is packaged for Windows x64.');
 const file=resolve(root,'runtime/cloudflared.exe'),sha='f096265ec2fcbe9bb6e2d64268db167ced3fcbb83d894bdb9e2fcdb26f2ea7e2';
 let bytes=await readFile(file).catch(()=>null);
 if(!bytes||createHash('sha256').update(bytes).digest('hex')!==sha){
  console.log('Downloading the optional online connector (first launch only)...');
  const r=await fetch('https://github.com/cloudflare/cloudflared/releases/download/2026.9.3/cloudflared-windows-amd64.exe',{signal:AbortSignal.timeout(180000)});
  if(!r.ok)throw new Error('Online connector download failed. Try again, or use Start-Chip-Arena.bat for local play.');
  bytes=Buffer.from(await r.arrayBuffer());if(createHash('sha256').update(bytes).digest('hex')!==sha)throw new Error('Online connector checksum did not match. Nothing was executed.');
  await mkdir(resolve(root,'runtime'),{recursive:true});await writeFile(file+'.download',bytes);await rename(file+'.download',file);
 }
 // An explicit empty config avoids interfering with any existing tunnel configuration.
 const config=resolve(root,'runtime/quick-tunnel.yml');await writeFile(config,'{}\n');
 const child=start(file,['tunnel','--config',config,'--no-autoupdate','--url',local],['ignore','pipe','pipe']);
 let buffer='',shown=false,announced=false;const timer=setTimeout(()=>{if(!shown){console.error('No online link received. Check your internet connection; local play is still available at '+local);}},45000);timer.unref();
 const output=chunk=>{const text=chunk.toString();process.stdout.write(text);buffer=(buffer+text).slice(-8000);const match=buffer.match(/https:\/\/[a-z0-9-]+\.trycloudflare\.com/);
  if(match&&!shown){shown=true;clearTimeout(timer);console.log('\nSHARE THIS GAME LINK: '+match[0]+'\nFriends: open this link, then Join Room. The host connects locally.\nKeep this window open. The link expires when you close it.\n');}
  if(match&&!announced&&buffer.includes('Registered tunnel connection')&&gameServer?.connected){announced=true;gameServer.send({type:'public-invite',url:match[0]});}
 };
 child.stdout.on('data',output);child.stderr.on('data',output);
}
try{
 const existing=await health();if(existing)throw new Error('Port '+port+' is already in use. Close the previous launcher before starting another.');
 await (await startLauncher(root)).applyUpdates(root);
 console.log('Starting Chip Arena. Keep this window open; Ctrl+C stops the server.');
 gameServer=start(process.execPath,['--disable-warning=ExperimentalWarning','server.mjs'],['inherit','inherit','inherit','ipc']);
 gameServer.on('message',message=>{if(message?.type==='public-invite-ready'&&/^https:\/\/[a-z0-9-]+\.trycloudflare\.com$/.test(message.url)){console.log('PUBLIC INVITE READY: '+message.url);if(online)open(local);}});
 let ready=false;for(let n=0;n<80&&!stopping;n++){const h=await health();if(h?.application==='chip-arena'){ready=true;break;}await new Promise(r=>setTimeout(r,150));}
 if(!ready)throw new Error('The server did not start. Check the error above.');
 console.log('\nYOUR GAME: '+local+'\n');
 try{for(const interfaces of Object.values(networkInterfaces()))for(const item of interfaces||[])if(item.family==='IPv4'&&!item.internal)console.log('Same Wi-Fi / LAN: http://'+item.address+':'+port);}catch{console.log('LAN addresses could not be listed. Your local game is still ready.');}
 if(online){console.log('\nOnline mode shares this game through a temporary public link. No account required.');await tunnel();}else open(local);
}catch(error){console.error('\n'+error.message);stop(1);}
