import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {createCharacterRig,recolorCharacterRig} from '../src/client/character-rig.js';
import {BlockCooldownTint} from '../src/client/block-cooldown-tint.js';
import {ProjectileGlow} from '../src/client/projectile-glow.js';
import type {Snapshot} from '../src/shared/simulation.js';
test('cooldown tint pulses only the cooling player and restores original shared materials when ready',()=>{
 const a=createCharacterRig('#ff6633'),b=createCharacterRig('#ff6633'),tint=new BlockCooldownTint(),head=a.group.getObjectByName('blank-head')! as T.Mesh,other=b.group.getObjectByName('blank-head')! as T.Mesh;
 const original=head.material as T.MeshLambertMaterial,base=original.color.clone();assert.equal(other.material,original);
 const cooling={hp:100,blockCd:3,blockTime:0};tint.update(a.group,cooling,0);const bright=(head.material as T.MeshLambertMaterial).color.clone();tint.update(a.group,cooling,1/3);const dark=(head.material as T.MeshLambertMaterial).color.clone();
 assert.ok(dark.r<bright.r*.5);assert.ok(original.color.equals(base));assert.equal(other.material,original);
 tint.update(a.group,{...cooling,blockCd:0},.6);assert.equal(head.material,original);assert.ok(original.color.equals(base));
 tint.update(a.group,{...cooling,blockTime:.2},.3);assert.equal(head.material,original,'active block stays bright');tint.clear();
});
test('first-person recoloring, death, and rig removal cannot leave darkened shared materials behind',()=>{
 const r=createCharacterRig('#ff6633'),tint=new BlockCooldownTint(),head=r.group.getObjectByName('blank-head')! as T.Mesh,cooling={hp:100,blockCd:2,blockTime:0};tint.update(r.group,cooling,1/3);
 recolorCharacterRig(r,'#ff9933','#66aaff');tint.update(r.group,cooling,1/3);tint.update(r.group,{...cooling,hp:0},1/3);assert.equal((head.material as T.MeshLambertMaterial).color.getHexString(),'66aaff');
 tint.update(r.group,cooling,1/3);tint.release(r.group);assert.equal((head.material as T.MeshLambertMaterial).color.getHexString(),'66aaff');
});
test('projectile halos are depth-tested and soft, light allocation stays bounded, and expired shots clean up',()=>{
 const scene=new T.Scene(),camera=new T.PerspectiveCamera(),glow=new ProjectileGlow(scene);
 const shots:Snapshot['projectiles']=Array.from({length:100},(_,i)=>({id:String(i),owner:'a',reflections:0,elements:[],pos:{x:i*.1,y:1,z:2},vel:{x:0,y:0,z:10},radius:.045,spec:i%2?'pistol':'fire'}));
 glow.update(shots,camera,0,1);assert.equal(glow.count,100);assert.equal(glow.lights.filter(l=>l.intensity>0).length,3);
 const sprite=scene.getObjectByName('projectile-glow')! as T.Sprite,material=sprite.material;assert.equal(material.blending,T.AdditiveBlending);assert.equal(material.toneMapped,false);assert.ok(material.depthTest);assert.equal(material.depthWrite,false);
 const data=(material.map as T.DataTexture).image.data as Uint8Array;assert.equal(data[3],0);assert.ok(data[(32*64+32)*4+3]>240);
 glow.update(shots.slice(0,2),camera,1,.3);assert.equal(glow.count,2);assert.equal(glow.lights.filter(l=>l.intensity>0).length,1);
 glow.update([],camera,2,1);assert.equal(glow.count,0);assert.ok(glow.lights.every(l=>l.intensity===0));glow.dispose();assert.equal(scene.children.length,0);
});
