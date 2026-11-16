// The rehearsal starts workerd directly on loopback. Both this process and the
// Wrangler D1 CLI use the same pinned Miniflare and disposable persistence path.
import {createRequire} from 'node:module';
import {readFileSync} from 'node:fs';
import {join} from 'node:path';
const require=createRequire(import.meta.url);
const {Miniflare,convertV4MiniflareOptions}=createRequire(require.resolve('wrangler/package.json'))('miniflare');
const [work,port]=process.argv.slice(2);
const cfg=JSON.parse(readFileSync(join(work,'wrangler.json'),'utf8'));
const worker=new Miniflare(convertV4MiniflareOptions({
 modules:true,modulesRoot:join(work,'bundle'),scriptPath:join(work,'bundle','account-worker.js'),compatibilityDate:cfg.compatibility_date,
 bindings:cfg.vars,d1Databases:{DB:cfg.d1_databases[0].database_id},resourcePersistencePath:join(work,'.wrangler','state','v3'),
 host:'127.0.0.1',port:Number(port)
}));
await worker.ready;
process.on('SIGTERM',async()=>{await worker.dispose();process.exit(0);});
