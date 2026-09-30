// Included ONLY in the isolated test ZIP built with --channel; never in the publishable payload.
const {app,session,dialog}=require('electron');
const {join}=require('node:path');
const {mkdirSync}=require('node:fs');
const root=join(process.resourcesPath,'..','TEST-PROFILE');
mkdirSync(root,{recursive:true});
app.setPath('userData',root);
app.setPath('sessionData',root);
app.whenReady().then(()=>{
  require('./test-remote-status.cjs').installRemoteStatusHandler(session,join(process.resourcesPath,'game'));
}).catch(error=>{dialog.showErrorBox('Chip Arena test channel',error.message);app.quit();});
require('./main.cjs');
