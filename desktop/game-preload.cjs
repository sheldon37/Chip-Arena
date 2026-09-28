const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('arenaAccount',{request:(action,body)=>ipcRenderer.invoke('arena:account',action,body)});

contextBridge.exposeInMainWorld('arenaDesktop',{openUpdater:()=>ipcRenderer.invoke('arena:open-updater')});
