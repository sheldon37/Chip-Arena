const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('arenaAccount',{request:(action,body)=>ipcRenderer.invoke('arena:account',action,body)});
contextBridge.exposeInMainWorld('arenaDesktop',{
 lanOpen:(id,url)=>ipcRenderer.invoke('arena:lan-open',id,url),lanSend:(id,data)=>ipcRenderer.send('arena:lan-send',id,data),lanClose:id=>ipcRenderer.send('arena:lan-close',id),
 onLanEvent:callback=>{const listener=(_event,value)=>callback(value);ipcRenderer.on('arena:lan-event',listener);return ()=>ipcRenderer.removeListener('arena:lan-event',listener);},
 openUpdater:()=>ipcRenderer.invoke('arena:open-updater'),hostRoom:online=>ipcRenderer.invoke('arena:host-room',online),leaveRoom:()=>ipcRenderer.invoke('arena:leave-room'),resolveRoom:value=>ipcRenderer.invoke('arena:resolve-room',value),
 onStatus:callback=>{const listener=(_event,text)=>callback(text);ipcRenderer.on('arena:status',listener);return ()=>ipcRenderer.removeListener('arena:status',listener);}
});
