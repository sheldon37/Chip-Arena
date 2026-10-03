/**
 * Development-only chip VFX lab (not in the shipped build). Runs the real authoritative Simulation in the page,
 * triggers one chip effect through the real engine path, and renders the real Renderer from a fixed camera as a
 * labelled frame strip, so on-impact / area chip visuals can be compared before and after a change.
 *
 * Query: fx=<scenario> (see SCENARIOS), frames, span (s after the trigger), cell (px), dist, height, quality (0..1),
 * compat=1 (compatibility renderer path). Sets window.labDone when the sheet is complete.
 */
import * as T from 'three';
import {Simulation,initPhysics} from '../../src/shared/simulation.js';
import {statsFor} from '../../src/shared/content.js';
import {v,add} from '../../src/shared/config.js';
import {ArenaRenderer as Renderer} from '../../src/client/renderer.js';

const q=new URLSearchParams(location.search);
const num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d;
const fx=q.get('fx')||'bomb',frames=q.has('times')?q.get('times')!.split(',').length:Math.max(1,num('frames',8)),span=num('span',1.2),cell=num('cell',260),aspect=num('aspect',1.4),cellW=Math.round(cell*aspect);
const dist=num('dist',8),height=num('height',2.6),start=num('start',0);
const sheet=document.getElementById('sheet') as HTMLCanvasElement,status=document.getElementById('status')!;
type Sim=Simulation;type P=ReturnType<Sim['players']['get']>&{};
/** Each scenario sets chips and triggers the effect through the engine at t=0; P is the effect centre. */
const P=v(0,1,4);
const SCENARIOS:Record<string,{chips?:Record<string,number>;spec?:string;run:(s:Sim,a:P,b:P)=>void;note:string}>={
 bomb:{note:'Wait For It (4 m chaos bomb)',chips:{'delayed-gratification':1},run:(s,a)=>{s.chaos.field(a,'bomb',P,4,.02,48);}},
 mine:{note:'Mine Sweeper ring (2.3 m mines)',chips:{'cluster-mine':1},run:(s,a,b)=>{s.chaos.mineRing(a,P);for(const f of s.chaos.fields.values())f.arm=-1;b.body.setTranslation(add(P,v(1.6,0,0)),true);}},
 implosion:{note:'Reverse Grenade (4 m implosion)',chips:{'implosion-bomb':1},run:(s,a)=>{s.chaos.field(a,'implosion',P,4,.25,30);}},
 artillery:{note:'Meteor Shower (3 × 2.8 m)',chips:{artillery:1},run:(s,a,b)=>{s.chaos.direct(a,b,P,v(0,0,1),'lab-art');}},
 thunder:{note:'Thunder Step (4 m strike)',chips:{thunderstep:1},run:(s,a)=>{s.chaos.dash(a,P);}},
 shrapnel:{note:'Shrapnel Protocol (1.8 m)',chips:{shrapnel:3},run:(s,a,b)=>{s.directHit(a,b,P,v(0,0,1),1,0,'lab-shr');}},
 comet:{note:'Comet Dash pulse (3 m)',chips:{comet:1},run:(s,a)=>{s.explode(a,P,3,a.stats.dashBlast,a.stats.dashBlast/14*380,'dashBlast',true);}},
 corpse:{note:'Corpse Party (3.5 m)',chips:{'corpse-party':1},spec:'fire',run:(s,a)=>{s.explode(a,P,3.5,30,300,'corpseParty',true);}},
 meteor:{note:'Meteor Foundry impact blast',chips:{meteor:1},spec:'fire',run:(s,a,b)=>{s.directHit(a,b,P,v(0,0,1),1,0,'lab-met');}},
 supernova:{note:'Supernova Guard (3 m pulse)',chips:{supernova:1},run:(s,a)=>{a.body.setTranslation(P,true);s.explode(a,P,3,45,1800,'blockPulse',true);}},
 vacuum:{note:'Vacuum Impact (5 m pull)',chips:{vacuum:1},run:(s,a)=>{s.vacuum(a,P,1400);}},
 static:{note:'Static Discharge (3 m)',chips:{static:2},run:(s,a,b)=>{s.explode(a,P,3,14,60,'static',false);void b;}},
 storm:{note:'Storm In A Jar (4 m field)',chips:{'lightning-rod':1},run:(s,a)=>{s.chaos.field(a,'storm',P,4,3,18);}},
 acid:{note:'Chemical Spill (3 m pool)',chips:{'acid-rain':1},run:(s,a)=>{s.chaos.field(a,'acid',P,3,4,12);}},
 fire:{note:'Bonfire Permit (2.5 m)',chips:{wildfire:1},run:(s,a)=>{s.chaos.field(a,'fire',P,2.5,3,15);}},
 frost:{note:'Deep Freeze (3.5 m)',chips:{blizzard:1},run:(s,a)=>{s.chaos.field(a,'frost',P,3.5,3,4);}},
 time:{note:'Pocket Slow-Mo (4 m bubble)',chips:{'time-warp':1},run:(s,a)=>{s.chaos.field(a,'time',P,4,4,0);}},
 blackhole:{note:'Event Horizon (5 m)',chips:{'black-hole':1},run:(s,a)=>{s.chaos.field(a,'black-hole',P,5,3.5,10);}},
 repulsion:{note:'Personal Space (3.5 m)',chips:{'repulsion-pad':1},run:(s,a)=>{s.chaos.field(a,'repulsion',P,3.5,3,0);}},
 heal:{note:'Friendly Fungus (2.5 m)',chips:{'med-garden':1},run:(s,a)=>{s.chaos.field(a,'heal',P,2.5,4,6);}},
 sticky:{note:'Sticky Situation (3 m)',chips:{'sticky-bomb':1},run:(s,a,b)=>{b.body.setTranslation(P,true);s.chaos.field(a,'bomb',P,3,.3,28,b.id);}},
 aftershock:{note:'Ground Pound (3.5 m)',chips:{aftershock:1},spec:'sword',run:(s,a)=>{s.chaos.field(a,'bomb',P,3.5,.2,20);}},
};
const scenario=SCENARIOS[fx];

async function main(){
 if(!scenario)throw new Error('unknown fx '+fx+'; one of '+Object.keys(SCENARIOS).join(','));
 await initPhysics();
 const s=new Simulation({sandbox:true,map:'foundry'});
 s.addPlayer('a','A',scenario.spec==='fire'?'magic':scenario.spec==='sword'?'melee':'ranged',null,'#ff6b35',{});s.addPlayer('b','B','ranged',null,'#5aa9ff',{});
 const a=s.players.get('a')!,b=s.players.get('b')!;if(scenario.spec)a.spec=scenario.spec as never;s.resetRound();s.phase='fight';
 for(const prop of [...s.props.values()])s.removeProp(prop.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 s.addProp({id:'crate',pos:v(2.6,.5,6.2),size:v(1,1,1),material:'wood',dynamic:true,mass:6});
 a.body.setTranslation(v(-6,1,-4),true);b.body.setTranslation(v(1.2,1,4.6),true);a.chips={...(scenario.chips||{})};a.stats=statsFor(a.chips,a.spec);a.hp=a.stats.hp;
 b.stats.hp=b.hp=100000;
 for(let i=0;i<30;i++)s.step();
 const canvas=document.createElement('canvas');canvas.width=cellW;canvas.height=cell;document.body.appendChild(canvas);canvas.style.display='none';
 const r=new Renderer(canvas) as unknown as Record<string,any>;
 r.effectsQuality=num('quality',1);if(q.get('compat')==='1')r.compatibility=true;r.shake=false;r.projectileTrails=true;
 const gl=r.renderer as T.WebGLRenderer;gl.setPixelRatio(1);gl.setSize(cellW,cell,false);
 const camera=r.camera as T.PerspectiveCamera;
 const dt=1/60,times=q.has('times')?q.get('times')!.split(',').map(Number):Array.from({length:frames},(_,i)=>start+(frames>1?i*span/(frames-1):0));
 sheet.width=frames*cellW;sheet.height=cell+22;const ctx=sheet.getContext('2d')!;ctx.font='12px ui-monospace,monospace';ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
 ctx.fillStyle='#e8eef2';ctx.fillText(`${fx} · ${scenario.note} · quality ${r.effectsQuality}${r.compatibility?' · compat':''}`,6,15);
 // Warm the renderer (props, rigs) before the trigger.
 for(let i=0;i<20;i++){s.step();r.update(s.snapshot(),'a',0,0,dt,false);}
 scenario.run(s,a,b);
 let t=0,frame=0;
 const shoot=()=>{camera.aspect=cellW/cell;camera.fov=55;camera.updateProjectionMatrix();camera.position.set(P.x+dist*.55,P.y+height,P.z+dist*.85);camera.lookAt(P.x,P.y+.4,P.z);gl.render(r.scene,camera);
  ctx.drawImage(canvas,frame*cellW,22);ctx.fillStyle='rgba(0,0,0,.55)';ctx.fillRect(frame*cellW,22,74,16);ctx.fillStyle='#e8eef2';ctx.fillText(`t=${t.toFixed(2)}`,frame*cellW+5,34);frame++;};
 while(frame<frames){
  if(t+1e-6>=times[frame]){r.update(s.snapshot(),'a',0,0,dt,false);shoot();continue;}
  s.step();t+=dt;r.update(s.snapshot(),'a',0,0,dt,false);
 }
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}
main().catch(e=>{status.textContent='ERROR '+(e as Error).message;console.error(e);(window as unknown as {labError:string}).labError=String((e as Error).stack||e);});
