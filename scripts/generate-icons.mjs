// Imported through tsx by the build. All paths below are verbatim Lucide geometry.
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {CHIP_ICONS,CLASS_ICONS,SPEC_ICONS,ACTION_ICONS,EXTRA_ICONS} from '../src/client/icon-map.ts';
const names=[...new Set([...Object.values(CHIP_ICONS),...Object.values(CLASS_ICONS),...Object.values(SPEC_ICONS),...Object.values(ACTION_ICONS),...EXTRA_ICONS])].sort();
const icons=Object.fromEntries(names.map(name=>{const svg=readFileSync(`node_modules/lucide-static/icons/${name}.svg`,'utf8');return [name,svg.slice(svg.indexOf('>',svg.indexOf('<svg'))+1,svg.lastIndexOf('</svg>')).trim()];}));
writeFileSync('src/client/icons.generated.ts','// Generated from lucide-static (ISC). See public/licenses/lucide.txt.\nexport const ICON_PATHS:Record<string,string>='+JSON.stringify(icons)+';\n');
mkdirSync('public/licenses',{recursive:true});writeFileSync('public/licenses/lucide.txt',readFileSync('node_modules/lucide-static/LICENSE'));console.log(`Prepared ${names.length} matching vector icons.`);
