// Emit Wrangler config containing public identifiers only. Always pass the printed
// path to the pinned CLI; there is deliberately no default deploy target.
import {readFileSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const stage=process.argv[2];
if(!['development','staging','production'].includes(stage))throw Error('Choose development, staging or production.');
const cfg=JSON.parse(readFileSync(new URL('wrangler.template.json',import.meta.url)));
cfg.name+='-'+stage;
cfg.vars.ACCOUNT_ENV=stage;
cfg.d1_databases[0].database_name=cfg.name;
if(stage==='development'){
 cfg.d1_databases[0].database_id='00000000-0000-4000-8000-000000000001';
 cfg.vars.ACCOUNT_STATE='active';
}else{
 const id=process.env['CHIP_D1_'+stage.toUpperCase()+'_ID'];
 if(!/^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(id||''))throw Error('Set the D1 ID for this environment.');
 if(id===process.env['CHIP_D1_'+(stage==='staging'?'PRODUCTION':'STAGING')+'_ID'])throw Error('Database IDs must be distinct.');
 if(!/^[a-f0-9]{32}$/i.test(process.env.CLOUDFLARE_ACCOUNT_ID||''))throw Error('Specify your personal CLOUDFLARE_ACCOUNT_ID.');
 cfg.account_id=process.env.CLOUDFLARE_ACCOUNT_ID;
 const name=process.env['CHIP_D1_'+stage.toUpperCase()+'_NAME']||cfg.name;
 if(name!==cfg.name&&!(name.startsWith(cfg.name+'-')&&/^[a-z0-9-]{1,63}$/.test(name)))throw Error('Database name must match the selected environment.');
 cfg.d1_databases[0].database_name=name;
 cfg.d1_databases[0].database_id=id;
 cfg.vars.ACCOUNT_STATE=process.env.CHIP_ACCOUNT_STATE||'maintenance';
 if(!['active','maintenance'].includes(cfg.vars.ACCOUNT_STATE))throw Error('Invalid account state.');
}
if(process.env.STEAM_APP_ID&&process.env.STEAM_TICKET_IDENTITY){
 cfg.vars.STEAM_APP_ID=process.env.STEAM_APP_ID;
 cfg.vars.STEAM_TICKET_IDENTITY=process.env.STEAM_TICKET_IDENTITY;
}
const path=fileURLToPath(new URL('wrangler.'+stage+'.local.json',import.meta.url));
writeFileSync(path,JSON.stringify(cfg,null,2)+'\n',{mode:0o600});
console.log(path);
