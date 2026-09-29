import {build} from 'esbuild';
import {zipSync,unzipSync} from 'three/addons/libs/fflate.module.js';
import {readFileSync,writeFileSync,mkdirSync,cpSync,rmSync,readdirSync,statSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve,basename} from 'node:path';

// Build the browser first: npm run build. The release output stays outside Git.
const output=resolve(process.env.CHIP_RELEASE_DIR||'releases');
const runtimeZip=process.env.CHIP_NODE_ZIP||'.cache/runtimes/node-v24.21.0-win-x64.zip';
if(!runtimeZip)throw new Error('Set CHIP_NODE_ZIP to the official node-v24.21.0-win-x64.zip.');
const bytes=readFileSync(runtimeZip),sha='158f7685b44de51f6c0df1d153526cbcd3e1bc739a8dfc607721cef75de9e541';
if(createHash('sha256').update(bytes).digest('hex')!==sha)throw new Error('Node runtime SHA256 mismatch.');
const folder=resolve(output,'Chip-Arena-Windows');rmSync(folder,{recursive:true,force:true});mkdirSync(folder,{recursive:true});
await build({entryPoints:['src/server/index.ts'],outfile:folder+'/server.mjs',bundle:true,platform:'node',format:'esm',target:'node24',banner:{js:"import {createRequire} from 'node:module';const require=createRequire(import.meta.url);"}});
cpSync('dist/client',folder+'/dist/client',{recursive:true,filter:source=>!source.replaceAll('\\','/').includes('/updates')});cpSync('drizzle',folder+'/drizzle',{recursive:true});mkdirSync(folder+'/launcher');cpSync('launcher/start.mjs',folder+'/launcher/start.mjs');cpSync('launcher/update.mjs',folder+'/launcher/update.mjs');cpSync('.cache/release/release.json',folder+'/release.json');
for(const file of ['Start-Chip-Arena.bat','Start-Online.bat','Update-Chip-Arena.bat','READ-ME.txt'])writeFileSync(folder+'/'+file,readFileSync('launcher/'+file,'utf8').replace(/\r?\n/g,'\r\n'));
cpSync('dist/client/chip-arena-source.zip',folder+'/chip-arena-source.zip');
mkdirSync(folder+'/THIRD-PARTY-LICENSES');for(const [dep,name]of [['three','THREE'],['ws','WS'],['@dimforge/rapier3d-compat','RAPIER']]){const files=readdirSync('node_modules/'+dep).filter(f=>/^license/i.test(f));for(const file of files)cpSync('node_modules/'+dep+'/'+file,folder+'/THIRD-PARTY-LICENSES/'+name+'-'+file);}
mkdirSync(folder+'/runtime');const runtime=unzipSync(bytes,{filter:f=>f.name.endsWith('/node.exe')||f.name.endsWith('/LICENSE')});
for(const [name,data]of Object.entries(runtime))writeFileSync(folder+'/runtime/'+(basename(name)==='LICENSE'?'NODE-LICENSE.txt':'node.exe'),data);
const entries={};function add(path,relative){for(const name of readdirSync(path)){const p=path+'/'+name,r=relative+'/'+name;if(statSync(p).isDirectory())add(p,r);else entries[r]=new Uint8Array(readFileSync(p));}}add(folder,'Chip-Arena-Windows');
const zip=output+'/Chip-Arena-Windows.zip';writeFileSync(zip,zipSync(entries,{level:6}));console.log(JSON.stringify({folder,zip,bytes:statSync(zip).size,sha256:createHash('sha256').update(readFileSync(zip)).digest('hex')}));
