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
const SCENARIOS:Record<string,{chips?:Record<string,number>;spec?:string;run:(s:Sim,a:P,b:P)=>void;note:string;later?:[number,(s:Sim,a:P,b:P)=>void][]}>={
 bomb:{note:'Wait For It (4 m chaos bomb)',chips:{'delayed-gratification':1},run:(s,a)=>{s.chaos.field(a,'bomb',P,4,.02,48);}},
 mine:{note:'Mine Sweeper ring (2.3 m mines)',chips:{'cluster-mine':1},run:(s,a,b)=>{s.chaos.mineRing(a,P);for(const f of s.chaos.fields.values())f.arm=-1;b.body.setTranslation(add(P,v(1.6,0,0)),true);}},
 implosion:{note:'Reverse Grenade (4 m implosion)',chips:{'implosion-bomb':1},run:(s,a)=>{s.chaos.field(a,'implosion',P,4,.25,30);}},
 artillery:{note:'Meteor Shower (3 × 2.8 m)',chips:{artillery:1},run:(s,a,b)=>{s.chaos.direct(a,b,P,v(0,0,1),'lab-art');}},
 thunder:{note:'Thunder Step (4 m strike)',chips:{thunderstep:1},run:(s,a)=>{s.chaos.dash(a,P);}},
 shrapnel:{note:'Shrapnel Protocol (1.8 m)',chips:{shrapnel:3},run:(s,a,b)=>{s.directHit(a,b,P,v(0,0,1),1,0,'lab-shr');}},
 comet:{note:'Comet Dash (roll blast)',chips:{comet:1},run:(s,a)=>{a.body.setTranslation(P,true);s.chaos.dash(a,P);if(!s.chaos.fields.size)s.explode(a,P,3,a.stats.dashBlast,a.stats.dashBlast/14*380,'dashBlast',true);}},
 freeze:{note:'Time Out (Block freeze)',chips:{'freezer-block':1},run:(s,a,b)=>{a.body.setTranslation(add(P,v(-1.5,0,-1)),true);b.body.setTranslation(P,true);s.world.step();s.chaos.block(a);}},
 wormhole:{note:'Pocket Portal arrival',chips:{wormhole:1},run:(s,a)=>{const crate=s.props.get('crate')!;a.body.setTranslation(add(P,v(-4,0,0)),true);s.world.step();s.chaos.direct(a,crate,P,v(1,0,0),'lab-portal');}},
 swap:{note:'Trading Places swap',chips:{'swap-shop':1},run:(s,a,b)=>{a.body.setTranslation(add(P,v(-3,0,0)),true);b.body.setTranslation(add(P,v(2,0,0)),true);s.world.step();s.chaos.direct(a,b,b.body.translation(),v(1,0,0),'lab-swap');}},
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
 // V sweep (design §19.2): chip events the engine emits for single-target procs, mirrored with the engine's payloads.
 kinetic:{note:'Kinetic Driver impact',chips:{kinetic:1},run:(s)=>{s.emit('impact',P,{source:'a',mods:{kinetic:1},color:0xffd88d});}},
 demolition:{note:'Demolition Module impact',chips:{demolition:1},run:(s)=>{s.emit('impact',P,{source:'a',mods:{demolition:1},color:0xffd88d});}},
 critical:{note:'Lucky Circuit critical',chips:{lucky:1},run:(s)=>{s.emit('critical',P,{source:'a',color:0xffd66c});}},
 catch:{note:'Return to Sender catch',chips:{boomerang:1},run:(s)=>{s.emit('catch',P,{source:'a',color:0x7fffe0});}},
 ricochet:{note:'Ricochet bounce',chips:{ricochet:1},run:(s)=>{s.emit('ricochet',P,{source:'a',mods:{ricochet:1},color:0xffcc49});}},
 rally:{note:'Pong rally (paddle flash + streak to B)',chips:{pong:1},run:(s,a,b)=>{b.body.setTranslation(add(P,v(-3,0,3)),true);s.world.step();s.emit('ricochet',add(P,v(-.2,.3,.2)),{source:'a',mods:{pong:1},color:0xff6475,message:'rally',from:P,target:'b'});void a;}},
 split:{note:'Splitter split',chips:{splitter:1},run:(s)=>{s.emit('split',P,{source:'a',mods:{splitter:1},color:0x96ffb7});}},
 rebound:{note:'Rebound Engine melee rebound',chips:{rebound:1},run:(s)=>{s.emit('meleeRebound',P,{source:'a',target:'b',amount:1800,radius:3});}},
 counterpulse:{note:'Counter Pulse (3 m)',chips:{'block-pulse':1},run:(s,a)=>{a.body.setTranslation(P,true);s.explode(a,P,3,6,360,'blockPulse',true);}},
 dashblast:{note:'Blast Dash (3 m)',chips:{'blast-dash':1},run:(s,a)=>{s.explode(a,P,3,14,380,'dashBlast',true);}},
 trampoline:{note:'Bounce House (3 m)',chips:{trampoline:1},run:(s,a)=>{s.explode(a,P,3,10,0,'trampoline',false);}},
 panic:{note:'Do Not Press (6 m nova + field)',chips:{'panic-button':1},run:(s,a)=>{a.body.setTranslation(P,true);s.world.step();a.hp=10;s.chaos.damaged(a,null,'direct');}},
 revenge:{note:'Return Address (3 m bomb)',chips:{'revenge-mail':1},run:(s,a)=>{s.chaos.field(a,'bomb',P,3,1,25,undefined,'revenge-mail');}},
 bubble:{note:'Pop Quiz (3 m bomb)',chips:{'bubble-mine':1},run:(s,a)=>{s.chaos.field(a,'bomb',P,3,1,25,undefined,'bubble-mine');}},
 proximity:{note:'Special Delivery (2.5 m mine)',chips:{'proximity-mail':1},run:(s,a,b)=>{s.chaos.field(a,'mine',P,2.5,6,32,undefined,'proximity-mail');for(const f of s.chaos.fields.values())f.arm=-1;b.body.setTranslation(add(P,v(1.4,0,0)),true);}},
 breadcrumbs:{note:'Exit Gift (2.8 m mine)',chips:{'bread-crumbs':1},run:(s,a)=>{s.chaos.field(a,'mine',P,2.8,5,24,undefined,'bread-crumbs');}},
 hotfoot:{note:'Hot Foot (3 m fire)',chips:{'hot-foot':1},run:(s,a)=>{s.chaos.field(a,'fire',P,3,3,15,undefined,'hot-foot');}},
 demolitioncharge:{note:'Load-Bearing Surprise (3 m bomb)',chips:{'demolition-charge':1},run:(s,a)=>{s.chaos.field(a,'bomb',P,3,1,18,undefined,'demolition-charge');}},
 timeout:{note:'Time Out field (5 m)',chips:{'freezer-block':1},run:(s,a)=>{s.chaos.field(a,'time',P,5,3,0,undefined,'freezer-block');}},
 arc:{note:'Static / lightning chain arc',chips:{static:1},run:(s)=>{s.emit('arc',P,{from:add(P,v(-3,.4,-2)),source:'a',color:0xc4b4ff});s.emit('arc',add(P,v(2,0,1)),{from:P,source:'a',color:0xc4b4ff});}},
 splash:{note:'Water bolt impact (weapon, legacy torus)',spec:'water',run:(s)=>{s.emit('impact',P,{source:'a',color:0x8de9ee});}},
 push:{note:'Legacy E push (removed by source)',run:(s)=>{s.emit('push',P,{source:'a',radius:5,color:0x8de9ee});}},
 pull:{note:'Legacy E pull (removed by source)',run:(s)=>{s.emit('pull',P,{source:'a',radius:5,color:0x8451a2});}},
 // Ability tags (design §13.1), synthetic until G/M land: fields through chaos.field, finales as authoritative events.
 frag:{note:'Frag Grenade (4.2 m, kinetic grey)',run:(s)=>{s.emit('explosion',P,{source:'a',radius:4.2,message:'frag'});}},
 rocket:{note:'Underbarrel Rocket (2.5 m, fire)',run:(s)=>{s.emit('explosion',P,{source:'a',radius:2.5,message:'rocket'});}},
 orbital:{note:'Orbital Mark on B (3 m, locks .2 s before impact)',run:(s,a,b)=>{b.body.setTranslation(P,true);s.world.step();s.chaos.field(a,'bomb',P,3,1.2,60,'b','orbital-mark',{lockAt:s.time+1,impulse:600,blastKind:'ability',cast:'c1'});}},
 meteorstrike:{note:'Meteor Strike (3.5 m, then burning ground)',spec:'fire',run:(s,a)=>{s.chaos.field(a,'bomb',P,3.5,1,70,undefined,'meteor-strike',{cast:'c1'});},later:[[1.02,(s,a)=>{s.chaos.field(a,'fire',P,3.2,3,12,undefined,'meteor-strike',{cast:'c1',damageKind:'burn'});}]]},
 flamelance:{note:'Flame Lance impact (white-gold)',spec:'fire',run:(s)=>{s.emit('explosion',P,{source:'a',radius:1.2,message:'flame-lance'});}},
 tidal:{note:'Tidal Wave hit (water blue)',spec:'water',run:(s)=>{s.emit('explosion',P,{source:'a',radius:2,message:'tidal-wave'});}},
 geyser:{note:'Geyser Wall (3 × 2.2 m, left to right)',spec:'water',run:(s,a)=>{for(let i=0;i<3;i++)s.chaos.field(a,'bomb',add(P,v(3*i-3,0,0)),2.2,.7+.35*i,30,undefined,'geyser-wall',{cast:'c1'});}},
 blinkstrike:{note:'Blink Strike (teleport + arc + 2 m shock cyan)',spec:'lightning',run:(s)=>{s.emit('teleport',P,{from:add(P,v(-5,0,-3)),source:'a',message:'blink-strike'});s.emit('arc',add(P,v(1.5,0,1)),{from:P,source:'a',color:0x9fd2ff});s.emit('explosion',add(P,v(1.5,0,1)),{source:'a',radius:2,message:'blink-strike'});}},
 ball:{note:'Ball Lightning burst (3 m shock violet)',spec:'lightning',run:(s)=>{s.emit('arc',add(P,v(2,0,1)),{from:P,source:'a',color:0x9fd2ff});s.emit('explosion',P,{source:'a',radius:3,message:'ball-lightning'});}},
 shackles:{note:'Shadow Shackles hit (void)',spec:'darkness',run:(s)=>{s.emit('explosion',P,{source:'a',radius:1.2,message:'shackles'});}},
 singularity:{note:'Singularity (5 m vortex, then implosion)',spec:'darkness',run:(s,a)=>{s.chaos.field(a,'black-hole',P,4.5,1,8,undefined,'singularity',{cast:'c1',force:40});},later:[[1.02,(s,a)=>{s.emit('explosion',add(P,v(0,.15,0)),{source:'a',radius:4,message:'singularity'});s.vacuum(a,add(P,v(0,.15,0)),1300);}]]},
 status:{note:'B slowed (soak), shackled and marked',run:(s,a,b)=>{b.body.setTranslation(P,true);s.world.step();b.slowed={factor:.45,until:s.time+3,kind:'shackles'};const c=s.players.get('c');if(c){c.body.setTranslation(add(P,v(-2.2,0,0)),true);c.slowed={factor:.8,until:s.time+3,kind:'soak'};}s.chaos.field(a,'bomb',P,3,2.5,60,'b','orbital-mark',{lockAt:s.time+2.3,cast:'c1'});}},
 worst:{note:'Pool pressure: 4 E finales + 3 ability fields + chip fields',run:(s,a)=>{for(const [i,k] of (['acid','fire','storm','frost'] as const).entries())s.chaos.field(a,k,add(P,v(-6+i*4,0,-5)),3,3,1);s.chaos.field(a,'black-hole',add(P,v(-5,0,3)),4.5,3,8,undefined,'singularity',{cast:'s'});s.chaos.field(a,'fire',add(P,v(5,0,3)),3.2,3,12,undefined,'meteor-strike',{cast:'m'});s.chaos.field(a,'bomb',add(P,v(0,0,6)),3,3,60,undefined,'orbital-mark',{cast:'o'});
  for(const [i,m] of (['meteor-strike','geyser-wall','frag','singularity'] as const).entries())s.emit('explosion',add(P,v(-4.5+i*3,0,0)),{source:'a',radius:m==='frag'?4.2:3.5,message:m});}},
};
const scenario=SCENARIOS[fx];

async function main(){
 if(!scenario)throw new Error('unknown fx '+fx+'; one of '+Object.keys(SCENARIOS).join(','));
 await initPhysics();
 const s=new Simulation({sandbox:true,map:'foundry'});
 s.addPlayer('a','A',scenario.spec==='fire'||scenario.spec==='water'||scenario.spec==='lightning'||scenario.spec==='darkness'?'magic':scenario.spec==='sword'?'melee':'ranged',null,'#ff6b35',{});s.addPlayer('b','B','ranged',null,'#5aa9ff',{});if(fx==='status')s.addPlayer('c','C','ranged',null,'#7dd87a',{});
 const a=s.players.get('a')!,b=s.players.get('b')!;if(scenario.spec)a.spec=scenario.spec as never;s.resetRound();s.phase='fight';
 for(const prop of [...s.props.values()])s.removeProp(prop.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 s.addProp({id:'crate',pos:v(2.6,.5,6.2),size:v(1,1,1),material:'wood',dynamic:true,mass:6});
 a.body.setTranslation(v(-6,1,-4),true);b.body.setTranslation(v(1.2,1,4.6),true);a.chips={...(scenario.chips||{})};a.stats=statsFor(a.chips,a.spec);a.hp=a.stats.hp;
 b.stats.hp=b.hp=100000;
 for(let i=0;i<30;i++)s.step();
 const canvas=document.createElement('canvas');canvas.width=cellW;canvas.height=cell;document.body.appendChild(canvas);canvas.style.display='none';
 const r=new Renderer(canvas) as unknown as Record<string,any>;
 // The renderer passes the local pilot to ChaosVisuals once INTEGRATION wires it (V change request); the lab does it here.
 {const cv=r.chaosVisuals;if(cv){const up=cv.update.bind(cv);cv.update=(st:unknown)=>up(st,'a');}}
 r.effectsQuality=num('quality',1);if(q.get('compat')==='1')r.compatibility=true;r.shake=false;r.projectileTrails=true;
 const gl=r.renderer as T.WebGLRenderer;gl.setPixelRatio(1);gl.setSize(cellW,cell,false);
 const camera=r.camera as T.PerspectiveCamera;
 const dt=1/60,times=q.has('times')?q.get('times')!.split(',').map(Number):Array.from({length:frames},(_,i)=>start+(frames>1?i*span/(frames-1):0));
 sheet.width=frames*cellW;sheet.height=cell+22;const ctx=sheet.getContext('2d')!;ctx.font='12px ui-monospace,monospace';ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
 ctx.fillStyle='#e8eef2';ctx.fillText(`${fx} · ${scenario.note} · quality ${r.effectsQuality}${r.compatibility?' · compat':''}`,6,15);
 // Warm the renderer (props, rigs) before the trigger.
 for(let i=0;i<20;i++){s.step();r.update(s.snapshot(),'a',0,0,dt,false);}
 scenario.run(s,a,b);const later=[...(scenario.later||[])];
 if(fx==='status'){const c=s.players.get('c');if(c)c.stats.hp=c.hp=100000;}
 let t=0,frame=0;
 const shoot=()=>{camera.aspect=cellW/cell;camera.fov=55;camera.updateProjectionMatrix();camera.position.set(P.x+dist*.55,P.y+height,P.z+dist*.85);camera.lookAt(P.x,P.y+.4,P.z);gl.render(r.scene,camera);
  ctx.drawImage(canvas,frame*cellW,22);ctx.fillStyle='rgba(0,0,0,.55)';ctx.fillRect(frame*cellW,22,74,16);ctx.fillStyle='#e8eef2';ctx.fillText(`t=${t.toFixed(2)}`,frame*cellW+5,34);frame++;};
 while(frame<frames){
  if(t+1e-6>=times[frame]){r.update(s.snapshot(),'a',0,0,dt,false);shoot();continue;}
  s.step();t+=dt;while(later.length&&t+1e-6>=later[0][0])later.shift()![1](s,a,b);r.update(s.snapshot(),'a',0,0,dt,false);
 }
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}
main().catch(e=>{status.textContent='ERROR '+(e as Error).message;console.error(e);(window as unknown as {labError:string}).labError=String((e as Error).stack||e);});
