import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {v,idleInput,CONFIG,SpecId} from '../src/shared/config.js';
import {WEAPONS,statsFor} from '../src/shared/content.js';
import {Predictor} from '../src/client/prediction.js';
await initPhysics();
function arena(){const s=new Simulation({sandbox:true}),a=s.addPlayer('a')!,b=s.addPlayer('b')!;s.phase='fight';for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(200,1,200),material:'foundation'});a.body.setTranslation(v(0,.86,-4),true);b.body.setTranslation(v(0,.86,0),true);for(let n=0;n<20;n++)s.step();return {s,a,b};}
function travel(spec:SpecId,chips:Record<string,number>,block=false,opposing=false,resist:Record<string,number>={}){
 const {s,a,b}=arena();a.spec=spec;a.chips=chips;a.stats=statsFor(chips);b.stats=statsFor(resist);const target=block?a:b,direction=block?-1:1;if(opposing)target.move=v(0,0,-direction*CONFIG.speed);const start=target.body.translation().z;
 if(block){b.stats=statsFor(chips);a.stats=statsFor(resist);s.beginBlock(b);s.directHit(a,b,b.body.translation(),v(0,0,1),WEAPONS[spec].damage,WEAPONS[spec].impulse,'blocked-swing');assert.equal(b.hp,100);}
 else s.directHit(a,b,b.body.translation(),v(0,0,1),1,WEAPONS[spec].impulse*a.stats.impulse,'push-shot');
 let distance=0;for(let n=1;n<=90;n++){s.setInput(target.id,{...idleInput(),seq:n,z:opposing?-direction:0});s.step();distance=Math.max(distance,(target.body.translation().z-start)*direction);}s.dispose();return distance;
}
test('knockback chips move real grounded bodies several meters and stacked shots overcome counter-steering',()=>{
 const single=travel('pistol',{kinetic:1}),stacked=travel('pistol',{kinetic:2}),opposed=travel('pistol',{kinetic:2},false,true);
 assert.ok(single>1.9&&single<2.2);assert.ok(stacked>4.6&&stacked<5.1);assert.ok(opposed>2.7);
 assert.ok(travel('pistol',{bulldozer:1})>2.5);assert.ok(travel('pistol',{repulsor:1})>4.8);
 assert.ok(travel('pistol',{kinetic:2},false,false,{anchor:1})<stacked*.55);
});
test('blocking sword and greatsword swings throws the attacker back; Rebound Engine increases the distance',()=>{
 assert.ok(travel('sword',{},true)>5);const heavy=travel('greatsword',{},true);assert.ok(heavy>7.4&&heavy<8.1);assert.ok(travel('greatsword',{},true,true)>5.4);assert.ok(travel('greatsword',{rebound:1},true)>13);
});
test('strong launches remain bounded and do not tunnel through solid cover',()=>{
 const {s,b}=arena();s.addProp({id:'wall',pos:v(0,3,3),size:v(12,6,.5),material:'foundation'});s.world.step();s.damage(b,1,'a','direct',v(0,0,1),1e6);
 for(let n=0;n<120;n++){s.step();assert.ok(b.body.translation().z<2.75,'capsule center cannot cross the near wall face');assert.ok(Math.abs(b.body.linvel().z)<=CONFIG.maxExternalSpeed+.001);}assert.ok(b.body.translation().z<2.35,'contact penetration resolves back outside cover');s.dispose();
});
test('upward chip kicks launch immediately and match the authoritative prediction path',()=>{
 const {s,b}=arena(),predictor=new Predictor();b.ext=v(0,8,20);const state=s.snapshot(),view=state.players.find(p=>p.id==='b')!;predictor.reconcile(state,view);const startY=b.body.translation().y;
 let peak=startY;for(let n=1;n<=70;n++){const input={...idleInput(),seq:n,z:-1};s.setInput('b',input);s.step();predictor.step(input,1/60,view);peak=Math.max(peak,b.body.translation().y);assert.ok(Math.abs(predictor.pos.z-b.body.translation().z)<.01);assert.ok(Math.abs(predictor.pos.y-b.body.translation().y)<.01);}
 assert.ok(peak-startY>1.25);assert.ok(b.body.translation().y<.9,'kick is consumed rather than repeatedly lifting the player');predictor.dispose();s.dispose();
});
