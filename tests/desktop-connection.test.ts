import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {readFileSync} from 'node:fs';
import {EventEmitter} from 'node:events';
import vm from 'node:vm';
const require=createRequire(import.meta.url);
const {connectWithRetry,openReachableWindow}=require('../desktop/connection.cjs');
const nextTurn=()=>new Promise<void>(resolve=>setImmediate(resolve));

test('fresh DNS failures retry and clear the app resolver cache until connected',async()=>{
 let clock=0,attempts=0,resets=0;const notices:number[]=[];
 const value=await connectWithRetry(async()=>{if(++attempts<3)throw Error('net::ERR_NAME_NOT_RESOLVED');return 'connected';},{now:()=>clock,sleep:async(ms:number)=>{clock+=ms;},resetDNS:async()=>{resets++;},onRetry:(n:number)=>notices.push(n)});
 assert.equal(value,'connected');assert.equal(attempts,3);assert.equal(resets,2);assert.deepEqual(notices,[1,2]);
});

test('persistent DNS failure is bounded and certificate failures never retry',async()=>{
 let clock=0,attempts=0;
 await assert.rejects(connectWithRetry(async()=>{attempts++;throw Error('ERR_NAME_NOT_RESOLVED');},{timeoutMs:5000,now:()=>clock,sleep:async(ms:number)=>{clock+=ms;}}),/Retry connection/);
 assert.equal(clock,5000);assert.equal(attempts,3);
 let resets=0;
 await assert.rejects(connectWithRetry(async()=>{throw Error('ERR_CERT_AUTHORITY_INVALID');},{resetDNS:async()=>{resets++;}}),/ERR_CERT_AUTHORITY_INVALID/);
 assert.equal(resets,0);
});

test('stopping during retry cancels without making another request',async()=>{
 const controller=new AbortController();let attempts=0;
 await assert.rejects(connectWithRetry(async()=>{attempts++;throw Error('ERR_NAME_NOT_RESOLVED');},{signal:controller.signal,onRetry:()=>controller.abort(Error('Launch cancelled.'))}),/Launch cancelled|abort/i);
 assert.equal(attempts,1);
});

function mockWindow(fetch:any){
 const loaded:string[]=[];let resets=0,stops=0;
 return {loaded,get resets(){return resets;},get stops(){return stops;},isDestroyed:()=>false,loadURL:async(url:string)=>{loaded.push(url);},webContents:{stop:()=>{stops++;},session:{fetch,clearHostResolverCache:async()=>{resets++;}}}};
}

test('tunnel gateway warmup is retried before the exact room invite is opened',async()=>{
 const requests:string[]=[];const win=mockWindow(async(url:string)=>{requests.push(url);return requests.length===1?{ok:false,status:503}:{ok:true,json:async()=>({application:'chip-arena'})};});
 const invite='https://fresh-invite.trycloudflare.com/?room=ABCD2345';
 await openReachableWindow(win,invite,{timeoutMs:5000});
 assert.deepEqual(requests,['https://fresh-invite.trycloudflare.com/health','https://fresh-invite.trycloudflare.com/health']);
 assert.deepEqual(win.loaded,[invite]);assert.equal(win.resets,1);
});

test('hung health probes time out as retryable errors and never navigate later',async()=>{
 let probes=0;
 const win=mockWindow(async(_url:string,{signal}:{signal:AbortSignal})=>{probes++;await new Promise((_,reject)=>signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true}));});
 await assert.rejects(openReachableWindow(win,'https://fresh-invite.trycloudflare.com/',{timeoutMs:20,attemptTimeoutMs:5}),/Retry connection.*ERR_TIMED_OUT/);
 assert.ok(probes>=2);assert.ok(win.resets>=1);assert.equal(win.loaded.length,0);assert.equal(win.stops,probes);
});

test('stopping cancels an in-flight health request immediately',async()=>{
 const controller=new AbortController();let requested=false;
 const win=mockWindow(async(_url:string,{signal}:{signal:AbortSignal})=>{requested=true;await new Promise((_,reject)=>signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true}));});
 const pending=openReachableWindow(win,'https://fresh-invite.trycloudflare.com/',{signal:controller.signal});
 assert.equal(requested,true);controller.abort(Error('Launch cancelled.'));
 await assert.rejects(pending,/Launch cancelled/);assert.equal(win.resets,0);assert.equal(win.loaded.length,0);
});

test('local games do not depend on public tunnel DNS or its health endpoint',async()=>{
 const win=mockWindow(()=>{throw Error('Unexpected remote request');});
 await openReachableWindow(win,'http://127.0.0.1:8788/');
 assert.deepEqual(win.loaded,['http://127.0.0.1:8788/']);assert.equal(win.resets,0);
});

test('desktop keeps its hosting process after failed load; both retry controls reuse the same tunnel',async()=>{
 const handlers=new Map<string,Function>(),windows:any[]=[],spawns:any[]=[],opened:string[]=[],messages:string[]=[];
 let failLoad=true;
 class Window extends EventEmitter{
  destroyed=false;visible=true;webContents:any;
  constructor(_options:any){super();windows.push(this);this.webContents=Object.assign(new EventEmitter(),{setWindowOpenHandler:()=>{},send:(_channel:string,text:string)=>messages.push(text),session:{setPermissionRequestHandler:()=>{},setPermissionCheckHandler:()=>{}}});}
  isDestroyed(){return this.destroyed;}show(){this.visible=true;}hide(){this.visible=false;}focus(){}loadURL(){return Promise.resolve();}
  destroy(){this.destroyed=true;this.emit('closed');}close(){this.destroy();}
 }
 const app=Object.assign(new EventEmitter(),{setName:()=>{},requestSingleInstanceLock:()=>true,whenReady:async()=>{},quit:()=>{}});
 const electron={app,BrowserWindow:Window,ipcMain:{handle:(name:string,handler:Function)=>handlers.set(name,handler)},Menu:{setApplicationMenu:()=>{},buildFromTemplate:()=>{}},clipboard:{writeText:()=>{}},dialog:{showErrorBox:()=>{}},protocol:{registerSchemesAsPrivileged:()=>{},handle:()=>{}},net:{}};
 const policy=require('../desktop/policy.cjs');
 const sandbox={require:(name:string)=>{
  if(name==='electron')return electron;
  if(name==='node:child_process')return {spawn:(exe:string,args:string[])=>{const child=Object.assign(new EventEmitter(),{pid:100,stdout:new EventEmitter(),stderr:new EventEmitter(),kill:()=>{}});spawns.push({exe,args,child});if(exe==='taskkill')setImmediate(()=>child.emit('exit',0));return child;}};
  if(name==='./policy.cjs')return {...policy,freePort:async()=>8788};
  if(name==='./connection.cjs')return {openReachableWindow:async(_win:any,url:string)=>{opened.push(url);if(failLoad)throw Error("ERR_NAME_NOT_RESOLVED (-105)");}};
  return require(name);
 },__dirname:'/app',process:{resourcesPath:'/resources',platform:'win32',env:{}},URL,AbortController,setTimeout,clearTimeout};
 vm.runInNewContext(readFileSync(new URL('../desktop/main.cjs',import.meta.url),'utf8'),sandbox);
 await nextTurn();
 const event={sender:windows[0].webContents,senderFrame:{url:'arena://launcher/index.html'}};
 const first=handlers.get('arena:launch')!(event,'online');
 const firstFailed=assert.rejects(first,/server is still running/);
 await nextTurn();assert.equal(spawns.length,1);
 spawns[0].child.stdout.emit('data','YOUR GAME: http://127.0.0.1:8788\nhttps://fresh-invite.trycloudflare.com\nRegistered tunnel connection');
 await firstFailed;
 assert.equal(spawns.length,1,'a failed page load must not taskkill the host');
 assert.equal(windows[1].destroyed,true);assert.equal(windows[0].visible,true);
 await assert.rejects(handlers.get('arena:launch')!(event,'online'),/server is still running/);
 assert.equal(spawns.length,1,'Host online retries its existing host instead of creating a second process');
 failLoad=false;await handlers.get('arena:retry')!(event);
 assert.deepEqual(opened,Array(3).fill('https://fresh-invite.trycloudflare.com/'));
 assert.equal(windows[0].visible,false);assert.equal(windows[3].visible,true);
 assert.ok(messages.some(text=>text.includes('Your server is still running')));
 await handlers.get('arena:stop')!(event);await nextTurn();
 assert.equal(spawns.length,2);assert.equal(spawns[1].exe,'taskkill');
 assert.deepEqual(Array.from(spawns[1].args),['/pid','100','/T','/F']);
});
