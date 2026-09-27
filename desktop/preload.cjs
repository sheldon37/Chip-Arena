const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('arena',{launch:mode=>ipcRenderer.invoke('arena:launch',mode),join:url=>ipcRenderer.invoke('arena:join',url),stop:()=>ipcRenderer.invoke('arena:stop'),copy:()=>ipcRenderer.invoke('arena:copy'),onStatus:callback=>{ipcRenderer.on('arena:status',(_event,text)=>callback(text));}});
