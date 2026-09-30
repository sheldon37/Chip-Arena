import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,readdirSync,statSync} from 'node:fs';
import {dirname,join,resolve} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';

// tools/visual-lab/lab.ts is outside tsconfig (not typechecked) and outside the Vite build, so a renamed or
// removed export would only fail in the browser. These checks keep its imports honest.
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const labDir=join(root,'tools','visual-lab');
const lab=readFileSync(join(labDir,'lab.ts'),'utf8');
const load=(spec:string)=>import(pathToFileURL(resolve(labDir,spec).replace(/\.js$/,'.ts')).href) as Promise<Record<string,unknown>>;

test('every value the visual lab imports from src exists',async()=>{
 const imports=[...lab.matchAll(/import\s*\{([^}]*)\}\s*from\s*'(\.\.\/\.\.\/src\/[^']+)'/g)];
 assert.ok(imports.length>=10,'lab.ts imports were not found');
 for(const [,list,spec] of imports){
  const mod=await load(spec);
  for(const raw of list.split(',').map(s=>s.trim()).filter(Boolean)){
   if(raw.startsWith('type '))continue;
   const name=raw.split(/\s+as\s+/)[0].trim();
   assert.ok(name in mod&&mod[name]!==undefined,`${spec} no longer exports ${name}`);
  }
 }
});

test('the lab-critical exports are callable',async()=>{
 const required:Record<string,string[]>={
  '../../src/client/character-rig.js':['createCharacterRig'],
  '../../src/client/character-animation.js':['animateCharacter','animationPreviewPlayer','attackPose'],
  '../../src/client/cosmetics.js':['dressRig'],
  '../../src/client/weapon-visuals.js':['buildWeapon','weaponModifiers','bodyModifiers'],
  '../../src/client/player-intros.js':['PlayerIntros'],
  '../../src/shared/intros.js':['introDuration'],
  '../../src/client/lobby-poses.js':['animateLobbyPose','lobbyPlayer'],
  '../../src/client/first-person-view.js':['poseFirstPersonView'],
  '../../src/client/pose-flags.js':['markFirstPersonRig'],
  '../../src/client/block-impacts.js':['recordBlockHit','clearBlockImpacts'],
  '../../src/client/arena-lighting.js':['arenaLighting'],
 };
 for(const [spec,names] of Object.entries(required)){
  const mod=await load(spec);
  for(const name of names)assert.equal(typeof mod[name],'function',`${spec} ${name} must be a function or class`);
 }
});

test('the visual lab stays out of the shipped client',()=>{
 const html=readFileSync(join(root,'index.html'),'utf8'),vite=readFileSync(join(root,'vite.config.ts'),'utf8');
 assert.doesNotMatch(html,/visual-lab|tools\//);assert.doesNotMatch(vite,/visual-lab|tools\//);
 const walk=(dir:string):string[]=>readdirSync(dir).flatMap(name=>{const path=join(dir,name);return statSync(path).isDirectory()?walk(path):[path];});
 for(const file of walk(join(root,'src')).filter(f=>/\.(ts|tsx|js|mjs|css|html)$/.test(f)))assert.doesNotMatch(readFileSync(file,'utf8'),/visual-lab/,`${file} must not reference the visual lab`);
});
