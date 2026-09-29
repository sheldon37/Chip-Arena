// Included ONLY in the isolated test ZIP; never in the publishable update payload.
const {app}=require('electron');
const {join}=require('node:path');
const {mkdirSync}=require('node:fs');
const root=join(process.resourcesPath,'..','TEST-PROFILE');
mkdirSync(root,{recursive:true});
app.setPath('userData',root);
app.setPath('sessionData',root);
require('./main.cjs');
