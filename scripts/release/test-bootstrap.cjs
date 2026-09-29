// Included ONLY in the isolated test ZIP; never in the publishable update payload.
const {app,session,dialog}=require('electron');
const {join}=require('node:path');
const {mkdirSync}=require('node:fs');
const root=join(process.resourcesPath,'..','TEST-PROFILE');
mkdirSync(root,{recursive:true});
app.setPath('userData',root);
app.setPath('sessionData',root);
// Register before main.cjs queues its ready callback and creates the game window.
app.whenReady().then(()=>{
  require('./test-update-status.cjs').installTestStatusHandler(session,join(process.resourcesPath,'game'));
}).catch(error=>{dialog.showErrorBox('Chip Arena test feed',error.message);app.quit();});
require('./main.cjs');
