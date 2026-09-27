import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import {glowTexture} from './projectile-glow.js';

export type LuminousActor={rig:CharacterRig;player:Pick<PlayerView,'id'|'color'|'hp'|'blockCd'|'blockTime'>;light?:boolean};
type Binding={rig:CharacterRig;material:T.SpriteMaterial;halos:T.Sprite[]};
/** Actual local light spill plus depth-tested halos. No full-screen haze, shared-material edits, or unbounded lights. */
export class ArmorGlow {
 readonly lights:T.PointLight[]=[];private texture=glowTexture();private bindings=new Map<string,Binding>();
 constructor(private scene:T.Scene){for(let i=0;i<2;i++){const light=new T.PointLight(0xffffff,0,3.6,2);light.name='armor-light';scene.add(light);this.lights.push(light);}}
 update(actors:LuminousActor[],time:number,quality:number,compatibility=false){
  const active=actors.filter(a=>a.player.hp>0);
  for(const[id,b]of this.bindings)if(!active.some(a=>a.player.id===id&&a.rig===b.rig)){this.release(b);this.bindings.delete(id);}
  let lightIndex=0;this.lights.forEach(l=>l.intensity=0);
  for(const {rig:r,player:p,light:spill=true}of active){let b=this.bindings.get(p.id);
   if(!b){const material=new T.SpriteMaterial({map:this.texture,transparent:true,opacity:.65,blending:T.AdditiveBlending,depthTest:true,depthWrite:false,toneMapped:false});const halos:T.Sprite[]=[];
    const attach=(joint:T.Group,x:number,y:number,z:number,size:number)=>{const halo=new T.Sprite(material);halo.name='armor-radiance';halo.position.set(x,y,z);halo.scale.set(size,size,1);joint.add(halo);halos.push(halo);};
    if(spill){attach(r.chest,0,.13,.19,.75);attach(r.chest,0,.12,-.20,.62);}attach(r.leftShoulder,.08,0,0,.52);attach(r.rightShoulder,-.08,0,0,.52);if(spill){attach(r.leftKnee,0,-.06,.15,.38);attach(r.rightKnee,0,-.06,.15,.38);}attach(r.leftHand,0,0,.10,.26);attach(r.rightHand,0,0,.10,.26);
    b={rig:r,material,halos};this.bindings.set(p.id,b);
   }
   const pulse=p.blockCd>0&&p.blockTime<=0?1-(1-(.70+.30*Math.cos(time*Math.PI*3)))*Math.min(1,p.blockCd/.25):1;
   b.material.color.set(p.color).lerp(new T.Color(0xd1edff),.26);b.material.opacity=(quality<.5?.43:.70)*pulse;b.halos.forEach(h=>h.visible=!compatibility&&quality>0);
   if(spill&&lightIndex<this.lights.length){const light=this.lights[lightIndex++];r.chest.updateWorldMatrix(true,false);light.position.copy(r.chest.localToWorld(new T.Vector3(0,.06,.20)));light.color.copy(b.material.color);light.intensity=compatibility||quality<=0?0:(quality<.5?4:8)*pulse;}
  }
 }
 private release(b:Binding){b.halos.forEach(h=>h.removeFromParent());b.material.dispose();}
 clear(){this.bindings.forEach(b=>this.release(b));this.bindings.clear();this.lights.forEach(l=>l.intensity=0);}
 dispose(){this.clear();this.lights.forEach(l=>l.removeFromParent());this.texture.dispose();}
 get count(){return this.bindings.size;}
}
