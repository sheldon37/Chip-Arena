import {test} from 'node:test';
import assert from 'node:assert/strict';
import {rememberKillEffect,restoreKillEffect} from '../src/client/kill-effect-sync.js';
import {cosmetics,type Cosmetics} from '../src/shared/cosmetics.js';
const storage=()=>{const data=new Map<string,string>();return {data,getItem:(key:string)=>data.get(key)??null,setItem:(key:string,value:string)=>{data.set(key,value);},removeItem:(key:string)=>{data.delete(key);}};};

test('legacy account fallback cannot replace Impact ragdoll after save, restart or repeated reload',()=>{
 const store=storage(),requested=cosmetics({killEffect:'impact-ragdoll'}),legacy=cosmetics({killEffect:'voxel-burst'});
 // Persist before the request, then record an older service's successful but lossy reply.
 assert.equal(rememberKillEffect(store,'a',requested.killEffect,legacy.killEffect,false),true);
 assert.equal(rememberKillEffect(store,'a',requested.killEffect,legacy.killEffect,false),true);
 for(let restart=0;restart<3;restart++){
  const restored=cosmetics({...legacy,killEffect:restoreKillEffect(store,'a',legacy.killEffect,false)});
  assert.equal(restored.killEffect,'impact-ragdoll');
 }
 assert.equal(legacy.killEffect,'voxel-burst','do not falsify the server acknowledgement');
});
test('failed cloud request preserves local intent; accounts remain isolated',()=>{
 const store=storage();rememberKillEffect(store,'a','impact-ragdoll','voxel-burst',false);
 // No acknowledgement arrives: the pre-request record must survive.
 assert.equal(restoreKillEffect(store,'a','voxel-burst',false),'impact-ragdoll');
 assert.equal(restoreKillEffect(store,'b','voxel-burst',false),'voxel-burst');
});
test('accepted saves and explicit Voxel breakup selections clear the pending choice',()=>{
 const store=storage();rememberKillEffect(store,'a','impact-ragdoll','voxel-burst',false);
 assert.equal(rememberKillEffect(store,'a','impact-ragdoll','impact-ragdoll',false),false);
 assert.equal(store.data.size,0);
 rememberKillEffect(store,'a','impact-ragdoll','voxel-burst',false);
 assert.equal(rememberKillEffect(store,'a','voxel-burst','voxel-burst',false),false);
 assert.equal(restoreKillEffect(store,'a','voxel-burst',false),'voxel-burst');
});
test('a later different cloud choice wins; acknowledged choices retire the fallback',()=>{
 const store=storage();rememberKillEffect(store,'a','impact-ragdoll','voxel-burst',false);
 assert.equal(restoreKillEffect(store,'a','confetti',false),'confetti');assert.equal(store.data.size,0);
 rememberKillEffect(store,'a','impact-ragdoll','voxel-burst',false);
 assert.equal(restoreKillEffect(store,'a','impact-ragdoll',false),'impact-ragdoll');assert.equal(store.data.size,0);
});
test('pending choices never grant DEV access and malformed records are discarded',()=>{
 const store=storage();assert.equal(rememberKillEffect(store,'a','dev-gibber','voxel-burst',false),false);
 rememberKillEffect(store,'a','dev-gibber','voxel-burst',true);
 assert.equal(restoreKillEffect(store,'a','voxel-burst',false),'voxel-burst');assert.equal(store.data.size,0);
 for(const raw of ['{',JSON.stringify({selected:'__proto__',remote:'voxel-burst'}),JSON.stringify({selected:'impact-ragdoll'})]){
  store.setItem('chip-arena-kill-effect-pending:a',raw);
  assert.equal(restoreKillEffect(store,'a','voxel-burst',false),'voxel-burst');assert.equal(store.data.size,0);
 }
 assert.equal(rememberKillEffect(store,'a','unknown' as Cosmetics['killEffect'],'voxel-burst',true),false);
});
