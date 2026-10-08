import {createHash} from 'node:crypto';
import {readdirSync,readFileSync,writeFileSync} from 'node:fs';
const hash=createHash('sha256');
function walk(path){for(const entry of readdirSync(path,{withFileTypes:true}).sort((a,b)=>a.name.localeCompare(b.name))){const name=path+'/'+entry.name;if(name.endsWith('build-info.generated.ts'))continue;if(entry.isDirectory())walk(name);else hash.update(name).update(readFileSync(name));}}
walk('src');walk('public');hash.update(readFileSync('package.json'));const build=hash.digest('hex').slice(0,24);
writeFileSync('src/client/build-info.generated.ts',`// Generated from the current game source and assets.\nexport const GAME_BUILD='${build}';\n`);
