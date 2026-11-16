// Wrap the actual published Sites Worker. Its assets, D1 migrations and transport
// code can be carried forward from the verified live version.
import {build} from 'esbuild';
import {readFileSync,writeFileSync,realpathSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const [input,output]=process.argv.slice(2);
if(!input||!output)throw Error('Usage: node build-bridge.mjs <published worker.js> <new staged worker.js>');
const legacy=realpathSync(input),routing=fileURLToPath(new URL('../../src/relay/account-routing.ts',import.meta.url));
const marker='chip-arena-account-bridge-v1';
if(readFileSync(legacy,'utf8').includes(marker))throw Error('The input already contains this routing bridge.');
const result=await build({stdin:{sourcefile:'account-bridge.js',resolveDir:process.cwd(),contents:`
 import legacy from ${JSON.stringify(legacy)};
 export * from ${JSON.stringify(legacy)};
 import {routeOldAccounts} from ${JSON.stringify(routing)};
 export default {...legacy,async fetch(request,env,ctx){
  const path=new URL(request.url).pathname;
  if(path.startsWith('/api/accounts/')||path==='/api/auth/login'){
   const response=await routeOldAccounts(request,env);
   if(response)return response;
  }
  return legacy.fetch(request,env,ctx);
 }};
`},bundle:true,format:'esm',platform:'browser',target:'es2022',external:['cloudflare:*','node:*'],
 banner:{js:'// '+marker},write:false});
writeFileSync(resolve(output),result.outputFiles[0].contents,{flag:'wx'});
