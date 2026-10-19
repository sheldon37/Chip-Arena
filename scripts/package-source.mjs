import {zipSync} from 'three/addons/libs/fflate.module.js';
import {readFileSync,writeFileSync,readdirSync,statSync,mkdirSync} from 'node:fs';
const entries={};function add(path){if(statSync(path).isDirectory()){for(const name of readdirSync(path))if(name!=='__pycache__')add(path+'/'+name);}else entries['chip-arena/'+path]=new Uint8Array(readFileSync(path));}
for(const path of ['AGENTS.md','CLAUDE.md','.node-version','.github','.openai/hosting.json','assets','db','drizzle','drizzle.config.ts','src','desktop','launcher','public','tests','scripts','docs','README.md','package.json','package-lock.json','tsconfig.json','tsconfig.server.json','vite.config.ts','index.html','.env.example','.gitignore','.dockerignore','Dockerfile'])add(path);
mkdirSync('dist/client',{recursive:true});writeFileSync('dist/client/chip-arena-source.zip',zipSync(entries,{level:6}));console.log('Editable source: dist/client/chip-arena-source.zip');
