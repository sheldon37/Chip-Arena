import * as T from 'three';
import {InkMaterial} from '../materials.js';
import {MINION_POOLS} from './look.js';

/**
 * Siren minion materials, shared by EVERY minion (no per-minion material or geometry). Each rig part keeps one mesh; its
 * look lives in the per-instance colours of its voxel layout (layouts.ts), which multiply the category's base colour:
 * rot mottling, char, wound flesh, cracks and eyes cost no extra draw calls. The base colours are the plain-box colours
 * that the SVG compatibility renderer (which ignores instancing) and low detail show.
 *
 * Glowing cells (burn embers, cracks in the dead armor, the burned-in eyes, the dying strips) are flagged in the instance
 * colour: a channel value >= GLOW_FLAG-.5 means "glow", and the real multiplier is the value minus GLOW_FLAG. The shader
 * adds the cell's own colour as emission, flickering per cell; the dying armor glow also drops out now and then for the
 * whole minion (its material pool), like failing power.
 */
export const GLOW_FLAG=16;
/** Shared clock for every minion shader (seconds); animateSirenMinion advances it. */
export const MINION_CLOCK={value:0};
export function tickMinionClock(time:number){if(Number.isFinite(time))MINION_CLOCK.value=time;}

/** sRGB palette. The shell is the pilot's armor gone ashen grey-green; the dying glow is the Siren's pale violet. */
export const MINION_PALETTE={
 shell:0xaeb2a0,edge:0x585a50,flex:0x4f3030,accent:0x5e5a66,strip:0x8f7fe0,scrap:0x6e5a4b,
 rotDark:0x5c5a40,rotOlive:0x8f9470,rotBrown:0x5d4936,ooze:0x9aa255,grime:0x5c5d50,blood:0x5a1a1c,
 char:0x0d0b0e,soot:0x2a2427,scorch:0x5a4436,ember:0xe8dcff,emberHot:0xff6a2c,
 crack:0x9d84ff,eye:0xf4f0ff,bone:0xddd3bc,
} as const;
export type MinionCategory='shell'|'edge'|'flex'|'accent'|'strip'|'scrap';

/** Lambert with the minion shader edits. A subclass, so clones (block tint, styled copies) keep them. */
export class MinionMaterial extends T.MeshLambertMaterial{
 readonly minionUniforms={uSelf:{value:.1},uGlow:{value:1.6},uFlicker:{value:.35},uPhase:{value:0},uTime:MINION_CLOCK};
 constructor(color:T.ColorRepresentation,opts:{self?:number;glow?:number;flicker?:number;phase?:number}={}){
  super({color});const u=this.minionUniforms;u.uSelf.value=opts.self??u.uSelf.value;u.uGlow.value=opts.glow??u.uGlow.value;u.uFlicker.value=opts.flicker??u.uFlicker.value;u.uPhase.value=opts.phase??0;this.patch();
 }
 private patch(){
  const u=this.minionUniforms;
  this.onBeforeCompile=shader=>{
   Object.assign(shader.uniforms,u);
   shader.vertexShader=shader.vertexShader.replace('#include <common>','#include <common>\nvarying vec3 vMinionCell;')
    .replace('#include <begin_vertex>','#include <begin_vertex>\n#ifdef USE_INSTANCING\nvMinionCell=(instanceMatrix*vec4(0.0,0.0,0.0,1.0)).xyz;\n#else\nvMinionCell=vec3(0.0);\n#endif');
   shader.fragmentShader=shader.fragmentShader.replace('#include <common>','#include <common>\nvarying vec3 vMinionCell;\nuniform float uSelf,uGlow,uFlicker,uPhase,uTime;')
    .replace('#include <color_fragment>',`float minionGlow=0.0;
#if defined( USE_COLOR )
 vec3 minionColor=vColor;if(minionColor.r>${(GLOW_FLAG-.5).toFixed(1)}){minionGlow=1.0;minionColor-=${GLOW_FLAG.toFixed(1)};}
 diffuseColor.rgb*=minionColor;
#endif`)
    .replace('#include <emissivemap_fragment>',`#include <emissivemap_fragment>
{float h=fract(sin(dot(vMinionCell,vec3(127.1,311.7,74.7))+uPhase*7.13)*43758.5453);
 float wave=0.5+0.5*sin(uTime*(4.0+9.0*h)+h*40.0+uPhase*3.0);
 float slot=floor(uTime*7.0+uPhase*11.0);float drop=step(0.86,fract(sin(slot*12.9898+uPhase*78.233)*43758.5453));
 float flick=mix(1.0,wave,uFlicker)*(1.0-drop*uFlicker*0.8);
 totalEmissiveRadiance+=diffuseColor.rgb*(uSelf+minionGlow*uGlow*flick);}`);
  };
 }
 override customProgramCacheKey(){return 'siren-minion';}
 override clone():this{const m=new (this.constructor as new(c:T.ColorRepresentation)=>this)(this.color).copy(this);const a=this.minionUniforms,b=m.minionUniforms;b.uSelf.value=a.uSelf.value;b.uGlow.value=a.uGlow.value;b.uFlicker.value=a.uFlicker.value;b.uPhase.value=a.uPhase.value;return m;}
}

/** Per-category settings: base colour, self light (keeps wounds and rot readable under the arena key), glow and flicker. */
const CATEGORY:Record<MinionCategory,{color:number;self:number;glow:number;flicker:number}>={
 shell:{color:MINION_PALETTE.shell,self:.18,glow:1.7,flicker:.35},
 edge:{color:MINION_PALETTE.edge,self:.1,glow:1.7,flicker:.35},
 flex:{color:MINION_PALETTE.flex,self:.14,glow:1.5,flicker:.35},
 accent:{color:MINION_PALETTE.accent,self:.1,glow:1.5,flicker:.85},
 strip:{color:MINION_PALETTE.strip,self:.2,glow:1.6,flicker:.9},
 scrap:{color:MINION_PALETTE.scrap,self:.06,glow:1.5,flicker:.3},
};
const pools=new Map<string,MinionMaterial>();
/** The shared material for a category and pool (pool = flicker phase). `charred` is the burning-away look of a dying minion. */
export function minionMaterial(category:MinionCategory,pool=0,charred=false){
 pool=((pool%MINION_POOLS)+MINION_POOLS)%MINION_POOLS;const key=category+pool+(charred?'c':'');let m=pools.get(key);
 if(!m){const c=CATEGORY[category],color=charred?new T.Color(c.color).lerp(new T.Color(MINION_PALETTE.char),.86):c.color;
  m=new MinionMaterial(color,{self:charred?.02:c.self,glow:charred?3.2:c.glow,flicker:charred?.6:c.flicker,phase:pool*1.618+(category==='accent'||category==='strip'?.37:0)});m.name=`siren-minion-${key}`;pools.set(key,m);}
 return m;
}
export const materialCount=()=>pools.size;
/** The base colour (linear) of a category: instance multipliers are relative to it. */
export function categoryColor(category:MinionCategory){return new T.Color(CATEGORY[category].color);}

/** Black-flame tongues: the Siren's ink with a strong pale rim and rising streaks, at minion scale. */
export const TONGUE_MATERIAL=new InkMaterial({color:0x0b0911},.5,.12);TONGUE_MATERIAL.name='siren-minion-tongue';
/** Emergence pool and death flames/ash: unlit cubes coloured per instance (Basic, so the SVG path accepts it). */
export const FX_MATERIAL=new T.MeshBasicMaterial({color:0xffffff});FX_MATERIAL.name='siren-minion-fx';
/** The unit cube for minion-owned meshes (tongues, scrap, effects). Not typed BoxGeometry, so consumers that read every
 * rig box as a body block (voxel burst, afterimages) skip them, as they skip wound cells. */
export const MINION_CUBE=new T.BufferGeometry().copy(new T.BoxGeometry(1,1,1));MINION_CUBE.name='siren-minion-cube';
