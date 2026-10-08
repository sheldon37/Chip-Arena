import {spawnSync} from 'node:child_process';
import {resolve} from 'node:path';
const output=resolve(process.env.CHIP_RELEASE_DIR||'releases');
const python=process.env.CHIP_PYTHON||'python';
const args=['scripts/package-desktop.py','--electron','.cache/runtimes/electron-v44.4.5-win32-x64.zip','--base',output+'/Chip-Arena-Windows.zip','--tunnel','.cache/runtimes/cloudflared-windows-amd64.exe','--out',output+'/Chip-Arena-Desktop.zip'];
const result=spawnSync(python,args,{stdio:'inherit',shell:false});
if(result.error)throw result.error;
process.exitCode=result.status??1;
