import * as T from 'three';

/** Siren palette: black flame, pale ash edges, white glow, siren-red only for danger telegraphs. */
export const PALETTE={ink:0x0a080e,smoke:0x17121f,ash:0xcfc6ea,glow:0xffffff,teeth:0xf3f0ff,ember:0xe6defa,warn:0xff2f44,warnDeep:0x8a0d1c} as const;

/** One clock for every Siren shader (seconds; the renderer's visual time). */
export const SIREN_CLOCK={value:0};
/** Global multiplier on the body's pale rim (the intro blackout dims it so only the eyes and grin remain). */
export const SIREN_RIM={value:1};
export function setSirenRim(k:number){SIREN_RIM.value=k;}
export function tickSirenClock(time:number){if(Number.isFinite(time))SIREN_CLOCK.value=time;}

/**
 * The apparition's body: an opaque Lambert surface (so voxel parts keep their sculpt) that is near black, with a pale
 * fresnel rim that keeps the silhouette readable on dark maps and slow rising streaks so the surface seems to burn.
 * clone() keeps the shader edits (BlockCooldownTint clones rig materials).
 */
export class InkMaterial extends T.MeshLambertMaterial{
 readonly sirenUniforms={uRim:{value:new T.Color(PALETTE.ash)},uRimStrength:{value:.2},uRimPower:{value:3.0},uFlow:{value:.025},uTime:SIREN_CLOCK,uRimScale:SIREN_RIM};
 constructor(params:T.MeshLambertMaterialParameters={},rim=.2,flow=.025){
  super({color:PALETTE.ink,...params});this.sirenUniforms.uRimStrength.value=rim;this.sirenUniforms.uFlow.value=flow;this.patch();
 }
 private patch(){
  const u=this.sirenUniforms;
  this.onBeforeCompile=shader=>{
   Object.assign(shader.uniforms,u);
   shader.vertexShader=shader.vertexShader.replace('#include <common>','#include <common>\nvarying vec3 vSirenWorld;')
    .replace('#include <worldpos_vertex>','#include <worldpos_vertex>\n{vec4 sw=vec4(transformed,1.0);\n#ifdef USE_INSTANCING\nsw=instanceMatrix*sw;\n#endif\nvSirenWorld=(modelMatrix*sw).xyz;}');
   shader.fragmentShader=shader.fragmentShader.replace('#include <common>','#include <common>\nvarying vec3 vSirenWorld;\nuniform vec3 uRim;uniform float uRimStrength,uRimPower,uFlow,uTime,uRimScale;')
    .replace('#include <emissivemap_fragment>',`#include <emissivemap_fragment>
{float facing=clamp(abs(dot(normalize(normal),normalize(vViewPosition))),0.0,1.0);float rim=pow(1.0-facing,uRimPower);
 vec3 w=vSirenWorld;float rise=sin(w.y*23.0-uTime*6.5+sin(w.x*13.0+uTime*1.7)*2.2+w.z*9.0);
 float flick=0.5+0.5*sin(w.x*31.0-w.z*27.0+uTime*3.1);float streak=smoothstep(.7,1.0,rise)*flick;
 totalEmissiveRadiance+=uRim*(rim*uRimStrength+streak*uFlow)*uRimScale;}`);
  };
  this.customProgramCacheKey=()=>'siren-ink';
 }
 override clone():this{const m=super.clone();(m as unknown as InkMaterial).copyUniforms(this);return m;}
 private copyUniforms(src:InkMaterial){this.sirenUniforms.uRim.value.copy(src.sirenUniforms.uRim.value);this.sirenUniforms.uRimStrength.value=src.sirenUniforms.uRimStrength.value;this.sirenUniforms.uRimPower.value=src.sirenUniforms.uRimPower.value;this.sirenUniforms.uFlow.value=src.sirenUniforms.uFlow.value;this.patch();}
}

const fogVertex='#include <fog_pars_vertex>',fogFragment='#include <fog_pars_fragment>';
/** Fog uniforms for ShaderMaterials with fog:true (cloned per material; the shared clock stays shared). */
const fogUniforms=()=>T.UniformsUtils.clone(T.UniformsLib.fog);
/**
 * Unlit glow (eyes, teeth, telegraphs). A ShaderMaterial, so rig-wide tints (block cooldown) leave it alone, and
 * toneMapped false so white stays white under ACES. `intensity` scales the colour; `flicker` adds a nervous shimmer.
 */
export function glowMaterial(color:T.ColorRepresentation,opts:{intensity?:number;flicker?:number;transparent?:boolean;opacity?:number;additive?:boolean;side?:T.Side;depthWrite?:boolean}={}){
 return new T.ShaderMaterial({
  uniforms:{...fogUniforms(),uColor:{value:new T.Color(color)},uIntensity:{value:opts.intensity??1},uFlicker:{value:opts.flicker??0},uOpacity:{value:opts.opacity??1},uTime:SIREN_CLOCK},
  // Instancing-aware, so glow instances (floor ticks) land where their matrices put them.
  vertexShader:`${fogVertex}\nvarying vec2 vUv;\nvoid main(){vUv=uv;vec4 p=vec4(position,1.0);\n#ifdef USE_INSTANCING\np=instanceMatrix*p;\n#endif\nvec4 mvPosition=modelViewMatrix*p;gl_Position=projectionMatrix*mvPosition;\n#include <fog_vertex>\n}`,
  fragmentShader:`${fogFragment}\nuniform vec3 uColor;uniform float uIntensity,uFlicker,uOpacity,uTime;varying vec2 vUv;\nvoid main(){float f=1.0-uFlicker*(0.5+0.5*sin(uTime*37.0+sin(uTime*11.0)*3.0));gl_FragColor=vec4(uColor*uIntensity*f,uOpacity);\n#include <fog_fragment>\n}`,
  transparent:opts.transparent??false,depthWrite:opts.depthWrite??!(opts.transparent??false),blending:opts.additive?T.AdditiveBlending:T.NormalBlending,side:opts.side??T.FrontSide,fog:true,toneMapped:false,
 });
}

/** Instanced unlit cubes coloured per instance: black flame tongues, embers, debris. */
export function particleMaterial(opts:{transparent?:boolean;additive?:boolean}={}){
 return new T.ShaderMaterial({
  uniforms:{...fogUniforms(),uOpacity:{value:1}},
  vertexShader:`${fogVertex}\nvarying vec3 vColor;\nvoid main(){vColor=vec3(1.0);\n#ifdef USE_INSTANCING_COLOR\nvColor=instanceColor;\n#endif\nvec4 mvPosition=modelViewMatrix*instanceMatrix*vec4(position,1.0);gl_Position=projectionMatrix*mvPosition;\n#include <fog_vertex>\n}`,
  fragmentShader:`${fogFragment}\nuniform float uOpacity;varying vec3 vColor;\nvoid main(){gl_FragColor=vec4(vColor,uOpacity);\n#include <fog_fragment>\n}`,
  transparent:opts.transparent??false,depthWrite:!(opts.transparent??false),blending:opts.additive?T.AdditiveBlending:T.NormalBlending,fog:true,toneMapped:false,
 });
}

/** A black-fire orb: dark core, pale fresnel corona, a churning surface. Used for palm flames and fireballs. */
export function orbMaterial(core:T.ColorRepresentation=PALETTE.ink,rim:T.ColorRepresentation=PALETTE.ash,rimStrength=1.4){
 return new T.ShaderMaterial({
  uniforms:{...fogUniforms(),uCore:{value:new T.Color(core)},uRim:{value:new T.Color(rim)},uRimStrength:{value:rimStrength},uTime:SIREN_CLOCK},
  vertexShader:`${fogVertex}\nvarying vec3 vN,vV,vP;\nvoid main(){vP=position;vec4 mvPosition=modelViewMatrix*vec4(position,1.0);vN=normalize(normalMatrix*normal);vV=-mvPosition.xyz;gl_Position=projectionMatrix*mvPosition;\n#include <fog_vertex>\n}`,
  fragmentShader:`${fogFragment}\nuniform vec3 uCore,uRim;uniform float uRimStrength,uTime;varying vec3 vN,vV,vP;\nvoid main(){float f=pow(1.0-clamp(dot(normalize(vN),normalize(vV)),0.0,1.0),2.2);float churn=0.5+0.5*sin(vP.y*14.0-uTime*9.0+sin(vP.x*11.0+uTime*4.0)*2.0);gl_FragColor=vec4(uCore+uRim*(f*uRimStrength+churn*f*0.6),1.0);\n#include <fog_fragment>\n}`,
  fog:true,toneMapped:false,
 });
}

let radial:T.Texture|undefined;
/** Soft radial alpha (white centre to transparent edge) for halos, shadow pools and glows. */
export function radialTexture(){
 if(radial)return radial;const size=64,data=new Uint8Array(size*size*4);
 for(let y=0;y<size;y++)for(let x=0;x<size;x++){const r=Math.hypot((x+.5)/size*2-1,(y+.5)/size*2-1),i=(y*size+x)*4;data[i]=data[i+1]=data[i+2]=255;data[i+3]=Math.round(255*Math.max(0,1-r)**1.6);}
 const t=new T.DataTexture(data,size,size);t.needsUpdate=true;t.magFilter=t.minFilter=T.LinearFilter;return radial=t;
}
/** Additive halo sprite material (tone mapping off, depth tested). */
export function haloMaterial(color:T.ColorRepresentation,opacity=.6){return new T.SpriteMaterial({map:radialTexture(),color,transparent:true,opacity,blending:T.AdditiveBlending,depthWrite:false,toneMapped:false});}
/** Dark soft-edged decal (shadow pools, scorch). */
export function decalMaterial(color:T.ColorRepresentation=0x000000,opacity=.6){return new T.MeshBasicMaterial({map:radialTexture(),color,transparent:true,opacity,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-2,polygonOffsetUnits:-2});}

/**
 * A licking flame shell for cylinders and spheres (eruption jets, explosions): dark tongues with pale edges, holes
 * that scroll upward, a fade uniform. uv.x runs around, uv.y up the shell.
 */
export function flameShellMaterial(ink:T.ColorRepresentation=PALETTE.ink,edge:T.ColorRepresentation=PALETTE.ash){
 return new T.ShaderMaterial({
  uniforms:{...fogUniforms(),uInk:{value:new T.Color(ink)},uEdge:{value:new T.Color(edge)},uFade:{value:1},uSpeed:{value:1},uTime:SIREN_CLOCK},
  vertexShader:`${fogVertex}\nvarying vec2 vUv;varying vec3 vN,vV;\nvoid main(){vUv=uv;vec4 mvPosition=modelViewMatrix*vec4(position,1.0);vN=normalize(normalMatrix*normal);vV=-mvPosition.xyz;gl_Position=projectionMatrix*mvPosition;\n#include <fog_vertex>\n}`,
  fragmentShader:`${fogFragment}\nuniform vec3 uInk,uEdge;uniform float uFade,uSpeed,uTime;varying vec2 vUv;varying vec3 vN,vV;
  void main(){float a=vUv.x*6.2831853,y=vUv.y,t=uTime*uSpeed;
   // Vertical tongues that wiggle as they rise; each has its own flickering tip height.
   float licks=0.5+0.5*sin(a*7.0+sin(y*4.0-t*6.0)*1.4+t*1.7);
   float tip=0.62+0.3*sin(a*5.0+t*7.0)*sin(a*3.0-t*3.3)+0.08*sin(a*11.0-t*9.0);
   float body=licks*0.55+0.45-smoothstep(tip-0.35,tip,y)*1.15;float alpha=smoothstep(0.32,0.52,body)*uFade;if(alpha<0.04)discard;
   float edge=1.0-smoothstep(0.0,0.12,abs(body-0.42));float fres=pow(1.0-abs(dot(normalize(vN),normalize(vV))),2.0);
   gl_FragColor=vec4(mix(uInk,uEdge,clamp(edge*0.8+fres*0.3,0.0,1.0)),alpha);\n#include <fog_fragment>\n}`,
  transparent:true,depthWrite:false,side:T.DoubleSide,fog:true,toneMapped:false,
 });
}
