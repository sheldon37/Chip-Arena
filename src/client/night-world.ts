import * as T from 'three';
import {arenaMap} from '../shared/maps.js';
/** Render-only surroundings: the playable bounds and fall rules stay authoritative. */
export class NightWorld {
 readonly group=new T.Group();private current='';private sky?:T.ShaderMaterial;private smoke?:T.Points;private smokeSeeds:number[]=[];
 constructor(scene:T.Scene){scene.add(this.group);this.group.name='night-world';}
 set(raw:unknown){const m=arenaMap(raw);if(m.id===this.current)return;this.clear();this.current=m.id;
 const cold=['moonlit','snow'].includes(m.theme),volcanic=m.theme==='forge',natural=cold||['castle','desert'].includes(m.theme),horizon=new T.Color(volcanic?0x633345:cold?0x405677:0x333f64),zenith=new T.Color(volcanic?0x100b1c:0x080e22);
 this.sky=new T.ShaderMaterial({side:T.BackSide,depthWrite:false,fog:false,uniforms:{time:{value:0},horizon:{value:horizon},zenith:{value:zenith},moonSize:{value:m.id==='moonlit'?.065:.028}},vertexShader:`varying vec3 direction;void main(){direction=position;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,fragmentShader:`
 varying vec3 direction;uniform float time,moonSize;uniform vec3 horizon,zenith;
 float hash(vec3 p){return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453);}
 float noise(vec3 p){vec3 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);float z0=mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x),mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y);float z1=mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x),mix(hash(i+vec3(0,1,1)),hash(i+vec3(1)),f.x),f.y);return mix(z0,z1,f.z);}
 float fbm(vec3 p){return noise(p)*.55+noise(p*2.03)*.27+noise(p*4.07)*.13+noise(p*8.1)*.05;}
 void main(){vec3 d=normalize(direction);float up=max(0.,d.y);vec3 col=mix(horizon,zenith,smoothstep(0.,.85,up));col=mix(col,horizon*.45,smoothstep(0.,.45,-d.y));
 vec3 md=normalize(vec3(-.4,.46,-.78));float moonDistance=length(d-md),halo=exp(-moonDistance*12.);col+=vec3(.16,.21,.3)*halo;float disc=1.-smoothstep(moonSize*.96,moonSize,moonDistance);float crater=.76+.24*fbm(d*170.);col=mix(col,vec3(.79,.86,1.)*crater,disc);
 vec3 stars=d*230.;vec3 cell=floor(stars);float star=step(.996,hash(cell))*pow(max(0.,1.-length(fract(stars)-.5)*2.),8.)*smoothstep(.08,.3,d.y);col+=vec3(.62,.72,.9)*star*(.8+.2*sin(time*.4+hash(cell)*30.));
 vec3 cloud=vec3(d.x/(abs(d.y)+.28)*2.+time*.008, .35, d.z/(abs(d.y)+.28)*2.+time*.004);float density=smoothstep(.40,.72,fbm(cloud))*smoothstep(-.04,.22,d.y);col=mix(col,mix(horizon*.85,vec3(.24,.29,.40),halo),density*.76);
 gl_FragColor=vec4(col,1.);#include <tonemapping_fragment>
 #include <colorspace_fragment>
 }`.replace('1.);#include','1.);\n#include')});
 const dome=new T.Mesh(new T.SphereGeometry(230,40,24),this.sky);dome.name='clouded-night-sky';dome.renderOrder=-100;dome.frustumCulled=false;this.group.add(dome);
 let seed=17;for(const c of m.id)seed=seed*31+c.charCodeAt(0);const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
 // A continuous lower landscape closes the horizon in every viewing direction.
 const segments=128,rings=9,positions:number[]=[],indices:number[]=[];for(let j=0;j<=rings;j++)for(let i=0;i<=segments;i++){const a=i/segments*Math.PI*2,r=43+j/rings*145,shape=Math.sin(a*5+.6)+Math.sin(a*11)*.45+Math.cos(a*17)*.2;positions.push(Math.sin(a)*r,-10+Math.max(0,j-1)*(.9+(natural?1.4:.3))+(j>1?shape*j*(natural?1.5:.6):0),Math.cos(a)*r);}for(let j=0;j<rings;j++)for(let i=0;i<segments;i++){const a=j*(segments+1)+i,b=a+segments+1;indices.push(a,b,a+1,b,b+1,a+1);}const terrain=new T.BufferGeometry();terrain.setAttribute('position',new T.Float32BufferAttribute(positions,3));terrain.setIndex(indices);terrain.computeVertexNormals();const land=new T.Mesh(terrain,new T.MeshStandardMaterial({color:cold?0x7589a4:volcanic?0x38212f:natural?0x38465c:0x252d41,roughness:1,emissive:cold?0x172338:0x11121e,emissiveIntensity:.45,side:T.DoubleSide}));land.name='surrounding-landscape';this.group.add(land);
 const block=new T.BoxGeometry(1,1,1),cone=new T.ConeGeometry(1,1,7),parts:{x:number;y:number;z:number;sx:number;sy:number;sz:number;ry:number}[]=[],lights:typeof parts=[],trees:typeof parts=[],stacks:typeof parts=[];
 for(let i=0;i<80;i++){const a=i/80*Math.PI*2,r=58+random()*55,x=Math.sin(a)*r,z=Math.cos(a)*r,h=natural?5+random()*14:12+random()*35,w=4+random()*5;
 if(natural&&i%3!==0){trees.push({x,y:h/2-5,z,sx:w*.65,sy:h,sz:w*.65,ry:a});continue;}
 parts.push({x,y:h/2-7,z,sx:w,sy:h,sz:w,ry:a});if(!natural||m.theme==='castle')for(let y=0;y<h-9;y+=3.5)for(const offset of [-.24,.24]){if(random()<.35)continue;lights.push({x:x-Math.sin(a)*(w/2+.025)+Math.cos(a)*w*offset,y,z:z-Math.cos(a)*(w/2+.025)-Math.sin(a)*w*offset,sx:.7,sy:1.1,sz:.035,ry:a});}
 if(['industrial','freight','reactor','forge'].includes(m.theme)&&i%8===0){stacks.push({x,y:h+1,z,sx:1.1,sy:10,sz:1.1,ry:0});for(let k=0;k<6;k++)this.smokeSeeds.push(x,h+6+k*2,z,random());}
 }
 const batch=(geo:T.BufferGeometry,data:typeof parts,color:number,glow=false)=>{if(!data.length)return;const mesh=new T.InstancedMesh(geo.clone(),glow?new T.MeshBasicMaterial({color}):new T.MeshStandardMaterial({color,roughness:.9,emissive:0x0e1526,emissiveIntensity:.3}),data.length),o=new T.Object3D();data.forEach((p,i)=>{o.position.set(p.x,p.y,p.z);o.scale.set(p.sx,p.sy,p.sz);o.rotation.y=p.ry;o.updateMatrix();mesh.setMatrixAt(i,o.matrix);});mesh.frustumCulled=false;this.group.add(mesh);};
 batch(block,parts,natural?0x364258:0x293449);batch(block,lights,cold?0xb1d5f1:volcanic?0xff7949:0xffcf8e,true);batch(cone,trees,cold?0x2d4156:0x202b40);batch(block,stacks,0x242d3a);block.dispose();cone.dispose();
 if(this.smokeSeeds.length){const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(new Float32Array(this.smokeSeeds.length/4*3),3));this.smoke=new T.Points(geo,new T.ShaderMaterial({transparent:true,depthWrite:false,uniforms:{color:{value:new T.Color(volcanic?0x745769:0x526077)}},vertexShader:`void main(){vec4 p=modelViewMatrix*vec4(position,1.);gl_PointSize=min(70.,800./max(1.,-p.z));gl_Position=projectionMatrix*p;}`,fragmentShader:`uniform vec3 color;void main(){float r=length(gl_PointCoord-.5)*2.;gl_FragColor=vec4(color,pow(max(0.,1.-r),2.)*.3);}`}));this.smoke.frustumCulled=false;this.group.add(this.smoke);}
 }
 update(time:number){if(this.sky)this.sky.uniforms.time.value=time;if(this.smoke){const a=this.smoke.geometry.getAttribute('position') as T.BufferAttribute;for(let i=0;i<a.count;i++){const n=i*4,age=(time*.7+this.smokeSeeds[n+3]*18)%18;a.setXYZ(i,this.smokeSeeds[n]+age*.28,this.smokeSeeds[n+1]+age,this.smokeSeeds[n+2]+Math.sin(age*.15+i)*1.5);}a.needsUpdate=true;}}
 private clear(){this.group.traverse(o=>{const m=o as T.Mesh;if(m.geometry)m.geometry.dispose();if(m.material)for(const mat of Array.isArray(m.material)?m.material:[m.material])mat.dispose();});this.group.clear();this.smoke=undefined;this.smokeSeeds=[];this.sky=undefined;}
 dispose(){this.clear();this.group.removeFromParent();}
}
