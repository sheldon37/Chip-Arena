// Execute the existing Electron victim pose expressions without a renderer; fixtures are independent of the C# port.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const fx=fs.readFileSync(path.join(root,'src/client/siren/fx.ts'),'utf8');
const contract=fs.readFileSync(path.join(root,'src/shared/siren-contract.ts'),'utf8');
const body=fx.slice(fx.indexOf('const age=time-g.at'),fx.indexOf('// Black flame climbs over the victim'));
if(!body.includes('victim.leftKnee.rotation.set'))throw Error('Siren victim source anchor changed');
const laugh=Function('return ('+contract.match(/laugh:(\{windup:[^\n]+\}),/)[1]+')')();
const names=['body','spine','chest','neck','head','leftArm','rightArm','leftElbow','rightElbow','leftLeg','rightLeg','leftKnee','rightKnee'];
const bones=[0,2,3,4,5,7,11,8,12,14,18,15,19];
const sample=Function('time','g','victim','siren','v3','sm','SIREN_TIMING',body);
const clamp=x=>Math.min(1,Math.max(0,x)),sm=x=>{x=clamp(x);return x*x*(3-2*x);};
const rows=[];
for(const at of [0,10,317.125])for(const age of [0,.1,.15,.3,.55,1,1.4,2,2.8,3.05,3.19]){
 const rotations=Object.fromEntries(names.map(n=>[n,[0,0,0]]));
 const vector=()=>({set(){}}),victim={group:{position:vector(),rotation:vector()}};
 for(const n of names)victim[n]={rotation:{set:(...v)=>rotations[n]=v},position:vector()};
 sample(at+age,{at,crushAt:at+laugh.crush},victim,{group:{rotation:{y:.37}}},{x:0,y:0,z:0},sm,{laugh});
 rows.push({time:at+age,at,crushAt:at+laugh.crush,bones,rotations:names.flatMap(n=>rotations[n])});
}
fs.writeFileSync(path.join(root,'unity/ChipArenaConversion/Tests/interaction-parity.json'),JSON.stringify({source:'src/client/siren/fx.ts updateGrabs',rows})+'\n');
console.log(`Exported ${rows.length} original Siren victim pose samples`);
