import {build} from 'esbuild';
import {mkdirSync,cpSync} from 'node:fs';
await build({entryPoints:['src/relay/worker.ts'],bundle:true,platform:'browser',format:'esm',target:'es2022',outfile:'dist/server/index.js'});
mkdirSync('dist/client/licenses',{recursive:true});cpSync('docs/NOBLE-HASHES-LICENSE.txt','dist/client/licenses/NOBLE-HASHES-LICENSE.txt');
mkdirSync('dist/.openai',{recursive:true});cpSync('.openai/hosting.json','dist/.openai/hosting.json');cpSync('drizzle','dist/.openai/drizzle',{recursive:true});
console.log('Built same-origin HTTPS room relay and isolated database migrations.');
