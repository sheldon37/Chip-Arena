// Build-time only. No JavaScript engine or original simulation runs inside Unity.
import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL,fileURLToPath} from 'node:url';
import {registerHooks,stripTypeScriptTypes} from 'node:module';
import {createHash} from 'node:crypto';
const project=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const source=path.resolve(process.argv[2]||'../../../..');
const lock=JSON.parse(fs.readFileSync(path.join(project,'tools/source-lock.json'),'utf8'));
for(const f of lock.files){const bytes=fs.readFileSync(path.join(source,f.path));const sha=createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');if(sha!==f.git_blob_sha)throw new Error('Source differs from pinned commit: '+f.path);}
const root=pathToFileURL(source+path.sep).href;
registerHooks({
 resolve(specifier,context,next){if(context.parentURL?.startsWith(root)&&specifier.endsWith('.js')){const ts=new URL(specifier.slice(0,-3)+'.ts',context.parentURL);if(fs.existsSync(ts))return {url:ts.href,shortCircuit:true};}return next(specifier,context);},
 load(url,context,next){if(url.startsWith(root)&&url.endsWith('.ts')){
  let code=fs.readFileSync(new URL(url),'utf8');
  // The old TS compiler erases these type-only imports. Node's syntax-only stripper needs them marked.
  if(url.endsWith('/shared/arena.ts'))code=code.replace('import {Material,Vec,v}', 'import {type Material,type Vec,v}');
  if(url.endsWith('/shared/maps.ts'))code=code.replace('import {v,Material}', 'import {v,type Material}');
  return {format:'module',source:stripTypeScriptTypes(code,{mode:'transform',sourceUrl:url}),shortCircuit:true};
 }return next(url,context);}
});
const get=p=>import(pathToFileURL(path.join(source,p)).href);
const content=await get('src/shared/content.ts'),config=await get('src/shared/config.ts');
const maps=await get('src/shared/maps.ts'),arena=await get('src/shared/arena.ts');
const gun=await get('src/shared/gun-kit.ts'),magic=await get('src/shared/magic-kit.ts');
const leet=await get('src/shared/leet-content.ts'),arcade=await get('src/shared/arcade-content.ts'),emperor=await get('src/shared/emperor-content.ts'),siren=await get('src/shared/siren-chips.ts');
const souls=await get('src/shared/souls-combat.ts'),hazards=await get('src/shared/stage-hazards.ts');
const pairs=o=>Object.entries(o).filter(([,v])=>typeof v==='number').map(([name,value])=>({name,value}));
const kits=[];for(const [spec,kit] of Object.entries({...gun.GUN_KITS,...magic.MAGIC_KITS}))for(const [slot,ability]of Object.entries(kit))if(slot==='q'||slot==='e')kits.push({spec,slot,...ability});
kits.push(...JSON.parse(fs.readFileSync(path.join(project,'tools/melee-abilities.json'),'utf8')));
for(const ability of JSON.parse(fs.readFileSync(path.join(project,'tools/ranged-abilities.json'),'utf8'))){
 const i=kits.findIndex(a=>a.spec===ability.spec&&a.slot===ability.slot);if(i<0)throw new Error('Missing ranged Q base');kits[i]=ability;
}
const catalogue={schema:1,sourceCommit:lock.commit,weapons:Object.values(content.WEAPONS),chips:content.CHIPS,
 baseStats:pairs(content.BASE_STATS),config:pairs(config.CONFIG),
 rarities:Object.entries(content.RARITIES).map(([id,data])=>({id,...data,leetWeight:leet.LEET_WEIGHTS[id]||0})),
 incompatibilities:[['leet',leet.LEET_INCOMPATIBLE],['ansatsuken',arcade.ARCADE_INCOMPATIBLE],['emperor',emperor.MISHIMA_INCOMPATIBLE]].map(([spec,ids])=>({spec,ids:[...ids]})),
 exclusiveOdds:[['ansatsuken',arcade.ARCADE],['emperor',emperor.EMPEROR],['siren',siren.SIREN_CHIP_ODDS]].map(([spec,o])=>({spec,guaranteed:o.guaranteedExclusives,chance:o.exclusiveChance})),
 maps:maps.ALL_MAPS.map(m=>({...m,rotation:maps.ROTATION_MAPS.some(r=>r.id===m.id),blocks:arena.arenaBlocks(m.id).map(b=>({...b,rotation:b.rotation||{x:0,y:0,z:0,w:1},support:b.support||[]}))})),
 materials:Object.entries(config.MATERIALS).map(([id,m])=>({id,...m,durability:Number.isFinite(m.durability)?m.durability:0,indestructible:!Number.isFinite(m.durability)})),
 spawns:Array.from({length:8},(_,i)=>arena.playerSpawn(i)),abilities:kits,ads:Object.entries(gun.ADS_PROFILES).map(([spec,data])=>({spec,...data})),weights:pairs(souls.WEAPON_WEIGHT)};
// Fixtures are outputs from original source functions, not outputs copied from the C# implementation.
const random=seed=>{let s=seed>>>0;return ()=>((s=(Math.imul(s,1664525)+1013904223)>>>0)/4294967296);};
const setups=[{}, {vital:3,overclock:4,frame:2},Object.fromEntries(content.CHIPS.map(c=>[c.id,c.max])),{'l44t-copy':1,'arcade-rush':1,'mishima-godfist':1,'siren-crown':1}];
const fixtures={sourceCommit:lock.commit,cases:[]};
for(const spec of Object.keys(content.WEAPONS))for(let index=0;index<setups.length;index++){
 const chips=setups[index],stats=content.statsFor(chips,spec),seed=12345+index;
 fixtures.cases.push({spec,seed,chips:Object.entries(chips).map(([id,count])=>({id,count})),stats:pairs(stats),eligible:content.eligibleChips(spec,chips).map(c=>c.id),offers:content.offerChips(spec,chips,random(seed),6),roll:souls.rollProfile(spec,stats)});
}
fixtures.hazards=['foundry','sawmill','spikevault','lavaworks'].flatMap(map=>[0,2.9,3.65,4.8,8.3].map(time=>({map,time,hazards:hazards.stageHazards(map,time)})));
const out=path.join(project,'Assets/Conversion/Resources');fs.mkdirSync(out,{recursive:true});
fs.writeFileSync(path.join(out,'GameContent.json'),JSON.stringify(catalogue)+'\n');
fs.writeFileSync(path.join(project,'Tests/source-parity.json'),JSON.stringify(fixtures)+'\n');
console.log(JSON.stringify({weapons:catalogue.weapons.length,chips:catalogue.chips.length,maps:catalogue.maps.length,blocks:catalogue.maps.reduce((n,m)=>n+m.blocks.length,0),abilities:kits.length,parityCases:fixtures.cases.length,sourceCommit:lock.commit}));
