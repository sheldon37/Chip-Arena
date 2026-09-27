const {app,BrowserWindow,ipcMain,Menu,clipboard,dialog,protocol,net:electronNet}=require('electron');
const {spawn}=require('node:child_process');
const {join}=require('node:path');
const {pathToFileURL}=require('node:url');
const {gameUrl,tunnelUrl,freePort}=require('./policy.cjs');
const {openReachableWindow}=require('./connection.cjs');
app.setName('Chip Arena');
protocol.registerSchemesAsPrivileged([{scheme:'arena',privileges:{standard:true,secure:true,supportFetchAPI:true}}]);
let home,game,child,closing=false,busy=false,stopping=false,shared='',pendingReject,lastTarget='',connectionAbort;
const root=join(process.resourcesPath,'game');
function status(text){if(home&&!home.isDestroyed())home.webContents.send('arena:status',String(text).slice(-1500));}
function stopServer(){return new Promise(resolve=>{connectionAbort?.abort(Error('Launch cancelled.'));const processToStop=child;child=null;shared='';lastTarget='';pendingReject?.(Error('Launch cancelled.'));pendingReject=null;if(!processToStop?.pid){resolve();return;}stopping=true;
 if(process.platform==='win32'){const killer=spawn('taskkill',['/pid',String(processToStop.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'});killer.once('exit',()=>{stopping=false;resolve();});killer.once('error',()=>{processToStop.kill();stopping=false;resolve();});}
 else{try{process.kill(-processToStop.pid,'SIGTERM');}catch{processToStop.kill();}stopping=false;resolve();}
 });}
function configureGame(win,url){
 const origin=new URL(url).origin;
 win.webContents.setWindowOpenHandler(()=>({action:'deny'}));
 win.webContents.on('will-navigate',(event,target)=>{try{if(new URL(target).origin!==origin)event.preventDefault();}catch{event.preventDefault();}});
 win.webContents.on('will-redirect',(event,target)=>{try{if(new URL(target).origin!==origin)event.preventDefault();}catch{event.preventDefault();}});
 win.webContents.session.setPermissionRequestHandler((contents,permission,callback)=>callback(contents===win.webContents&&['pointerLock','fullscreen','clipboard-sanitized-write'].includes(permission)));
 win.webContents.session.setPermissionCheckHandler((contents,permission)=>contents===win.webContents&&['pointerLock','fullscreen','clipboard-sanitized-write'].includes(permission));
 win.webContents.on('render-process-gone',()=>{status('Game renderer stopped. Return to the launcher and reopen the game.');if(!home.isDestroyed())home.show();});

}
async function openGame(raw){const url=gameUrl(raw);if(game&&!game.isDestroyed()){game.focus();return;}
 lastTarget=url;const controller=new AbortController();connectionAbort=controller;let entered=false;
 game=new BrowserWindow({show:false,width:1440,height:900,minWidth:960,minHeight:600,title:'Chip Arena',backgroundColor:'#060810',autoHideMenuBar:true,webPreferences:{nodeIntegration:false,contextIsolation:true,sandbox:true,webSecurity:true,partition:'persist:chip-arena-game'}});
 const current=game;configureGame(current,url);current.on('closed',()=>{if(game===current)game=null;if(!closing){home.show();if(entered){status('Game closed.');void stopServer();}}});
 try{status('Checking game connection…');await openReachableWindow(current,url,{signal:controller.signal,onRetry:(attempt,seconds)=>status(`Waiting for the server address… retry ${attempt} · up to ${seconds}s remaining. Keep the host running.`)});controller.signal.throwIfAborted();entered=true;current.show();home.hide();}
 catch(e){if(!current.isDestroyed())current.destroy();if(!closing)home.show();throw e;}
 finally{if(connectionAbort===controller)connectionAbort=null;}
}
async function retryConnection(){if(busy||stopping)throw Error('A connection attempt is already running.');if(!lastTarget)throw Error('Host online or paste an invite first.');busy=true;try{await openGame(lastTarget);status(shared?'Hosting: '+shared:'Connected.');return true;}catch(e){const error=Error(e.message+(child?' Your server is still running.':''),{cause:e});status(error.message);throw error;}finally{busy=false;}}

async function launch(online){if(online&&child&&shared&&!game&&!busy&&!stopping)return retryConnection();if(busy||stopping||child||game)throw Error('Close the current game before starting another.');busy=true;status(online?'Starting your online server…':'Starting local play…');
 try{const port=await freePort(),local='http://127.0.0.1:'+port;const exe=join(root,'runtime','node.exe');
 const target=await new Promise((resolve,reject)=>{let output='',ready=false,finished=false;const timer=setTimeout(()=>finish(Error('Server startup timed out. Check your connection and try again.')),120000);
 const finish=(error,url)=>{if(finished)return;finished=true;clearTimeout(timer);pendingReject=null;error?reject(error):resolve(url);};pendingReject=error=>finish(error);
 child=spawn(exe,[join(root,'launcher','start.mjs'),...(online?['--online']:[])],{cwd:root,env:{...process.env,PORT:String(port),CHIP_NO_BROWSER:'1'},windowsHide:true,detached:process.platform!=='win32',stdio:['ignore','pipe','pipe']});
 const consume=chunk=>{const text=chunk.toString();output=(output+text).slice(-8000);if(text.includes('Updating Chip Arena'))status('Downloading the latest game update…');if(text.includes('YOUR GAME:')){ready=true;status(online?'Connecting your public invite…':'Local server ready.');}const tunnel=tunnelUrl(output);if(online&&tunnel&&output.includes('Registered tunnel connection')){shared=tunnel;finish(null,tunnel);}else if(!online&&ready)finish(null,local);};
 const launched=child;child.stdout.on('data',consume);child.stderr.on('data',consume);child.once('error',e=>finish(e));child.once('exit',code=>{if(!finished)finish(Error('Server could not start. '+output.slice(-700)));else if(!closing&&!stopping&&child){status('Server stopped. Reopen the game to restart it.');home.show();}if(child===launched)child=null;});
 });await openGame(target);status(shared?'Hosting: '+shared:'Local play is running.');return {url:target};
 }catch(e){if(!online||!child||!lastTarget)await stopServer();const error=Error(e.message+(child?' Your server is still running; use Retry connection.':''),{cause:e});status(error.message);throw error;}finally{busy=false;}}
function validSender(event){if(event.sender!==home?.webContents||event.senderFrame?.url!=='arena://launcher/index.html')throw Error('Request not authorized.');}
if(!app.requestSingleInstanceLock())app.quit();else{
 app.on('second-instance',()=>{const win=game||home;if(win){if(win.isMinimized())win.restore();win.show();win.focus();}});
 app.whenReady().then(()=>{
 protocol.handle('arena',request=>{const url=new URL(request.url);if(url.hostname!=='launcher'||!['/index.html','/launcher.css','/launcher.js'].includes(url.pathname))return new Response('Not found',{status:404});return electronNet.fetch(pathToFileURL(join(__dirname,url.pathname.slice(1))).toString());});
 home=new BrowserWindow({width:1000,height:740,minWidth:880,minHeight:640,title:'Chip Arena',backgroundColor:'#090c14',autoHideMenuBar:true,webPreferences:{preload:join(__dirname,'preload.cjs'),nodeIntegration:false,contextIsolation:true,sandbox:true}});
 home.webContents.setWindowOpenHandler(()=>({action:'deny'}));home.webContents.on('will-navigate',event=>event.preventDefault());home.on('closed',()=>app.quit());home.loadURL('arena://launcher/index.html');
 Menu.setApplicationMenu(Menu.buildFromTemplate([{label:'Game',submenu:[{label:'Launcher',click:()=>home.show()},{label:'Retry connection',click:()=>{void retryConnection().catch(e=>status(e.message));}},{label:'Copy server link',click:()=>{if(shared)clipboard.writeText(shared);else status('Host online to create a public server link.');}},{type:'separator'},{role:'quit'}]},{label:'View',submenu:[{role:'togglefullscreen'},{role:'resetZoom'},{role:'zoomIn'},{role:'zoomOut'}]}]));
 ipcMain.handle('arena:launch',async(event,mode)=>{validSender(event);if(!['local','online'].includes(mode))throw Error('Unknown launch mode.');return launch(mode==='online');});
 ipcMain.handle('arena:join',async(event,url)=>{validSender(event);if(busy||stopping||child||game)throw Error('Close the current game first.');busy=true;try{await openGame(url);return true;}catch(e){status(e.message);throw e;}finally{busy=false;}});
 ipcMain.handle('arena:retry',async event=>{validSender(event);return retryConnection();});
 ipcMain.handle('arena:copy',event=>{validSender(event);if(shared)clipboard.writeText(shared);return shared;});
 ipcMain.handle('arena:stop',async event=>{validSender(event);if(game&&!game.isDestroyed())game.close();await stopServer();status('Server stopped.');return true;});
 }).catch(e=>{dialog.showErrorBox('Chip Arena',e.message);app.quit();});
 app.on('before-quit',event=>{if(closing)return;event.preventDefault();closing=true;void stopServer().finally(()=>app.quit());});
 app.on('window-all-closed',()=>app.quit());
}
