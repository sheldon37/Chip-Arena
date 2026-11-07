// The source's line icons (client/icons.generated.ts, Lucide ISC) and its icon language (client/icon-map.ts: chip, class,
// spec and action icons), exported verbatim for the native rasteriser in NativeIcons.cs. The Lucide licence text travels
// with the data in the player build. Data only.
// Usage: node tools/export_icons.mjs <pinned source>
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL,fileURLToPath} from 'node:url';import {registerHooks,stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const project=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);const inventory=JSON.parse(fs.readFileSync(path.join(project,'tools/conversion-inventory.json'),'utf8'));const root=pathToFileURL(source+path.sep).href;
const pinned=rel=>{const bytes=fs.readFileSync(path.join(source,rel)),entry=inventory.files.find(f=>f.path===rel),sha=createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');if(!entry||entry.gitBlob!==sha)throw Error('Pinned source mismatch '+rel);return bytes;};
registerHooks({load(url,c,next){if(url.startsWith(root)&&url.endsWith('.ts')){const rel=path.relative(source,fileURLToPath(url));return {format:'module',source:stripTypeScriptTypes(pinned(rel).toString(),{mode:'transform'}),shortCircuit:true};}return next(url,c);}});
const get=p=>import(pathToFileURL(path.join(source,'src/client',p+'.ts')).href);
const {ICON_PATHS}=await get('icons.generated'),map=await get('icon-map');pinned('src/client/icons.ts');
const used=new Set([...Object.values(map.CHIP_ICONS),...Object.values(map.CLASS_ICONS),...Object.values(map.SPEC_ICONS),...Object.values(map.ACTION_ICONS),...map.EXTRA_ICONS]);
const missing=[...used].filter(id=>!ICON_PATHS[id]);if(missing.length)throw Error('Icons without paths: '+missing.join(', '));if(!ICON_PATHS.cpu)throw Error('Fallback icon cpu is missing');
const pairs=o=>Object.entries(o).sort(([a],[b])=>a<b?-1:a>b?1:0).map(([id,icon])=>({id,icon}));
const data={commit:inventory.sourceCommit,license:pinned('public/licenses/lucide.txt').toString(),strokeWidth:1.7,icons:Object.keys(ICON_PATHS).sort().map(id=>({id,markup:ICON_PATHS[id]})),chips:pairs(map.CHIP_ICONS),classes:pairs(map.CLASS_ICONS),specs:pairs(map.SPEC_ICONS),actions:pairs(map.ACTION_ICONS)};
fs.writeFileSync(path.join(project,'Assets/Conversion/Resources/Icons.json'),JSON.stringify(data)+'\n');
console.log('Icons exported',data.icons.length,'icons',data.chips.length,'chip mappings',data.actions.length,'actions');
