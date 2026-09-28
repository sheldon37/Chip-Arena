const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('arenaAccount',{request:(action,body)=>ipcRenderer.invoke('arena:account',action,body)});
