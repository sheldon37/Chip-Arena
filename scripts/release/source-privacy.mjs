// TEST-ONLY feeds never carry the editable game source: no chip-arena-source.zip (or any ZIP), no src/ tree,
// no source maps. Decodes schema 1 (latest.json) and schema 2 (latest-v2.json) without trusting the launcher.
// Usage: node source-privacy.mjs <feed dir>...  (exit 1 and list the offending payload paths).
import {readFileSync,existsSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {gunzipSync,brotliDecompressSync} from 'node:zlib';
import {fileURLToPath} from 'node:url';
const SOURCE_MAP=/\.(m|c)?js\.map$|\.css\.map$/;
/** A payload path that would publish source. The game payload has no legitimate ZIP, so any ZIP counts. */
export function privatePath(path){const parts=String(path).replaceAll('\\','/').toLowerCase().split('/'),name=parts.at(-1);return name.endsWith('.zip')||parts.slice(0,-1).includes('src')||SOURCE_MAP.test(name);}
/** Offending payload paths in a feed directory, per manifest. */
export function feedProblems(dir){
 const problems=[],read=name=>JSON.parse(readFileSync(join(dir,name),'utf8'));
 if(existsSync(join(dir,'latest.json'))){const m=read('latest.json');if(m.file&&existsSync(join(dir,m.file))){const payload=JSON.parse(gunzipSync(readFileSync(join(dir,m.file))));for(const f of payload.files||[])if(privatePath(f.path))problems.push('latest.json: '+f.path);}}
 if(existsSync(join(dir,'latest-v2.json'))){const m=read('latest-v2.json');if(m.file&&existsSync(join(dir,m.file))){const body=brotliDecompressSync(readFileSync(join(dir,m.file))),length=body.readUInt32BE(8),index=JSON.parse(body.subarray(12,12+length));for(const f of index.files||[])if(privatePath(f.path))problems.push('latest-v2.json: '+f.path);}}
 return problems;
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 let failed=!process.argv[2];
 for(const dir of process.argv.slice(2)){const problems=feedProblems(resolve(dir));if(problems.length){failed=true;console.log(dir+': source content: '+problems.join(', '));}else console.log(dir+': no source archive, src/ tree or source map');}
 process.exitCode=failed?1:0;
}
