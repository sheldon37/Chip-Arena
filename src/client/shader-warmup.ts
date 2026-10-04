import * as T from 'three';

/**
 * GPU program warm-up and retention (lag-spike fix 2026-10-04). Linking a new WebGL program blocks the main thread
 * (tens to hundreds of ms on ANGLE/D3D11): a full-screen freeze. Three.js links lazily on the first draw of a new
 * material variant and deletes a program as soon as the last material using it is disposed, so per-event effect
 * materials (projectile and melee trails, impact glows, death fireworks, Siren rings) re-linked their program
 * whenever the previous effect of that kind had expired.
 *
 *  - retain(): every program the renderer links stays linked for the session (bounded by the number of variants).
 *  - ShaderMaterial anchors: the first disposed compiled material of each custom shader source is kept, so the shader's
 *    stage ids (part of the program cache key) stay stable and later materials of that source reuse the program.
 *  - request()/update(): on a new scene composition, compiles everything in the scene (pooled, still hidden effects
 *    included) plus hidden probes for the built-in material variants that effects create per event, with the scene's own lights and fog. Every material's
 *    program key includes the light counts, so when a Siren can throw a seeker (which carries its own point light),
 *    the scene is also compiled once with one extra point light.
 * Presentation only: nothing here changes what is drawn.
 */
type ProgramLike={usedTimes:number};
const PIN_CAP=512;
let anchorsInstalled=false;
const anchored=new Set<string>();
/** Keep the first disposed compiled ShaderMaterial of each shader source alive in three's caches (one per source). */
function installShaderAnchors(){
 if(anchorsInstalled)return;anchorsInstalled=true;
 // three asks a material for customProgramCacheKey() while building its program parameters, right after registering
 // its shader stages: that marks a material whose disposal would release them. (Anchoring a material that was never
 // compiled would hold nothing.)
 const compiled=new WeakSet<T.Material>(),key=T.ShaderMaterial.prototype.customProgramCacheKey,dispose=T.ShaderMaterial.prototype.dispose;
 T.ShaderMaterial.prototype.customProgramCacheKey=function(this:T.ShaderMaterial){compiled.add(this);return key.call(this);};
 T.ShaderMaterial.prototype.dispose=function(this:T.ShaderMaterial){
  if(compiled.has(this)&&!(this as T.ShaderMaterial&{isRawShaderMaterial?:boolean}).isRawShaderMaterial){const source=this.vertexShader+'\u0000'+this.fragmentShader;if(!anchored.has(source)){anchored.add(source);return;}}
  dispose.call(this);
 };
}

/** Built-in material variants that effects create per event (observed with a program-link trace in PvP and PvE). */
function probeMaterials(texture:T.Texture):[T.Material,'mesh'|'rgba'|'sprite'|'points'|'line'][]{
 const add=T.AdditiveBlending,dbl=T.DoubleSide,list:[T.Material,'mesh'|'rgba'|'sprite'|'points'|'line'][]=[];
 for(const side of [T.FrontSide,dbl]){
  list.push([new T.MeshBasicMaterial({vertexColors:true,transparent:true,blending:add,side,toneMapped:false}),'mesh']);
  list.push([new T.MeshBasicMaterial({map:texture,transparent:true,blending:add,side,toneMapped:false}),'mesh']);
  list.push([new T.MeshBasicMaterial({transparent:true,side}),'mesh']);
  list.push([new T.MeshBasicMaterial({transparent:true,side,toneMapped:false}),'mesh']);
 }
 list.push([new T.MeshBasicMaterial({vertexColors:true,transparent:true,toneMapped:false}),'mesh']);
 list.push([new T.MeshBasicMaterial({vertexColors:true,transparent:true}),'mesh']);
 list.push([new T.MeshBasicMaterial({vertexColors:true,transparent:true,toneMapped:false}),'rgba']);
 list.push([new T.MeshBasicMaterial({map:texture,transparent:true}),'mesh']);
 list.push([new T.MeshBasicMaterial({map:texture,transparent:true,toneMapped:false,fog:false}),'mesh']);
 list.push([new T.MeshBasicMaterial({transparent:true,toneMapped:false,fog:false}),'mesh']);
 list.push([new T.MeshBasicMaterial({toneMapped:false,fog:false}),'mesh']);
 list.push([new T.MeshBasicMaterial({toneMapped:false}),'mesh']);
 list.push([new T.MeshBasicMaterial(),'mesh']);
 list.push([new T.MeshLambertMaterial(),'mesh']);
 list.push([new T.SpriteMaterial({map:texture,transparent:true,blending:add,toneMapped:false}),'sprite']);
 list.push([new T.SpriteMaterial({map:texture,transparent:true,toneMapped:false}),'sprite']);
 list.push([new T.PointsMaterial({transparent:true}),'points']);
 list.push([new T.PointsMaterial({transparent:true,toneMapped:false,fog:false}),'points']);
 list.push([new T.LineBasicMaterial({transparent:true,toneMapped:false,fog:false}),'line']);
 return list;
}

export class ShaderWarmup {
 readonly probes=new T.Group();
 private pinned=new WeakSet<object>();private key='';private frames=0;private extraLight=false;private seekerLight=new T.PointLight(0xffffff,0,9,2);
 constructor(private renderer:T.WebGLRenderer,private scene:T.Scene){
  installShaderAnchors();
  this.probes.name='shader-warmup';this.probes.visible=false;
  const texture=new T.DataTexture(new Uint8Array([255,255,255,255]),1,1);texture.needsUpdate=true;
  const plane=new T.PlaneGeometry(.01,.01),count=plane.attributes.position.count;
  const rgb=plane.clone().setAttribute('color',new T.BufferAttribute(new Float32Array(count*3).fill(1),3));
  const rgba=plane.clone().setAttribute('color',new T.BufferAttribute(new Float32Array(count*4).fill(1),4));
  const points=new T.BufferGeometry().setAttribute('position',new T.Float32BufferAttribute([0,0,0,0,.01,0],3));
  for(const [material,kind] of probeMaterials(texture)){
   const o=kind==='sprite'?new T.Sprite(material as T.SpriteMaterial):kind==='points'?new T.Points(points,material):kind==='line'?new T.Line(points,material):new T.Mesh(kind==='rgba'?rgba:(material as T.MeshBasicMaterial).vertexColors?rgb:plane,material);
   o.frustumCulled=false;this.probes.add(o);
  }
  scene.add(this.probes);
 }
 /** Pins every program not pinned yet (a WeakSet check per program). Call after rendering. */
 retain(){
  const programs=(this.renderer.info as unknown as {programs:ProgramLike[]|null}).programs;
  // The cap only guards against an unforeseen source of ever-new variants (a few hundred are expected at most).
  if(programs&&programs.length<=PIN_CAP)for(const p of programs)if(!this.pinned.has(p)){this.pinned.add(p);p.usedTimes++;}
 }
 /** Requests a warm-up for a new scene composition `key` (map, mode, round); runs a couple of frames later so the
  *  frame that changed the scene has built its rigs and effect pools. */
 request(key:string,extraPointLight=false){key+=extraPointLight?'|+1':'';if(key!==this.key){this.key=key;this.frames=2;this.extraLight=extraPointLight;}}
 /** Call once per frame with the camera the scene is drawn with. */
 update(camera:T.Camera){
  if(this.frames<=0||--this.frames>0)return;
  // compile() creates and links every missing program without drawing; drivers with parallel shader compilation
  // finish the link in the background. (compileAsync is not used: its readiness poll throws once an effect material
  // is disposed before its program is ready.) compile() gathers the lights synchronously, so the extra light is
  // gone again before anything is drawn.
  const run=()=>{try{this.renderer.compile(this.scene,camera);}catch{/* a failed warm-up only means a later link */}};
  run();if(this.extraLight){this.scene.add(this.seekerLight);try{run();}finally{this.scene.remove(this.seekerLight);}}
  this.retain();
 }
}
