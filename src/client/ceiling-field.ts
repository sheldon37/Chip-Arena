import * as T from 'three';
import type {GameEvent} from '../shared/simulation.js';
/** Bounded local holograms. The rest of the ceiling stays completely invisible. */
export class CeilingFieldVisuals {
 readonly group=new T.Group();private hits:{mesh:T.Mesh;born:number}[]=[];private geometry=new T.PlaneGeometry(7,7);
 constructor(scene:T.Scene){this.group.name='ceiling-field-impacts';scene.add(this.group);}
 event(e:GameEvent,time:number){if(e.type!=='ceiling-hit')return;if(this.hits.length>=24){const old=this.hits.shift()!;old.mesh.removeFromParent();(old.mesh.material as T.Material).dispose();}
 const mat=new T.ShaderMaterial({transparent:true,depthWrite:false,side:T.DoubleSide,blending:T.AdditiveBlending,uniforms:{age:{value:0},tint:{value:new T.Color(e.color||0x75ddff)}},vertexShader:`varying vec2 uvField;void main(){uvField=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,fragmentShader:`varying vec2 uvField;uniform float age;uniform vec3 tint;
 void main(){vec2 p=(uvField-.5)*7.;float d=length(p),radius=.2+age*4.5;float ring=exp(-pow((d-radius)/.09,2.));vec2 cell=abs(fract(p*2.)-.5);float grid=1.-smoothstep(.015,.06,min(cell.x,cell.y));float reveal=(1.-smoothstep(radius-.1,radius+.3,d))*exp(-d*.65);float fade=pow(max(0.,1.-age/1.05),1.5);float flash=exp(-age*18.)*exp(-d*3.);float scan=.65+.35*sin(p.x*16.+age*19.);gl_FragColor=vec4(tint,(grid*reveal*.36*scan+ring*.85+flash)*fade);}`});
 const mesh=new T.Mesh(this.geometry,mat);mesh.rotation.x=-Math.PI/2;mesh.position.set(e.pos.x,e.pos.y-.025,e.pos.z);this.group.add(mesh);this.hits.push({mesh,born:time});}
 update(time:number){for(let i=this.hits.length-1;i>=0;i--){const h=this.hits[i],age=time-h.born;if(age<0||age>1.05){h.mesh.removeFromParent();(h.mesh.material as T.Material).dispose();this.hits.splice(i,1);}else(h.mesh.material as T.ShaderMaterial).uniforms.age.value=age;}}
 clear(){for(const h of this.hits)(h.mesh.material as T.Material).dispose();this.group.clear();this.hits=[];}
 dispose(){this.clear();this.geometry.dispose();this.group.removeFromParent();}
}
