// Owner bug 2026-10-04: "we want the sights to line up where we shoot". ADS shots must land on the crosshair ray (the
// eye ray the sights are aligned to) at 10, 25 and 50 m, through the real Simulation (eye-origin camera, muzzle spawn,
// gravity). Spread is zeroed so only the aim/ballistics alignment is measured.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {idleInput,v,add,sub,len,mul,forward,type SpecId,type Vec} from '../src/shared/config.js';
import {VIEW} from '../src/shared/view.js';
await initPhysics();
const GUNS=['pistol','machinegun','rifle'] as const;
function range(spec:SpecId,distance:number,ads:boolean){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A','ranged')!;a.spec=spec;s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(80,1,140),material:'foundation'});s.addProp({id:'wall',pos:v(0,6,distance+.25),size:v(40,14,.5),material:'foundation'});
 a.body.setTranslation(v(0,.9,0),true);s.world.step();a.grounded=true;
 const eye=add(a.body.translation(),v(0,VIEW.eyeHeight,0)),aimPoint=v(.6,eye.y+.4,distance),d=sub(aimPoint,eye);
 a.input={...idleInput(),yaw:Math.atan2(d.x,d.z),pitch:Math.atan2(d.y,Math.hypot(d.x,d.z)),camera:'third'};if(ads)a.adsSince=s.time-2;
 const tuning=s.abilities.shotTuning.bind(s.abilities);s.abilities.shotTuning=(o:Player)=>({...tuning(o),spread:0});
 s.projectiles.clear();s.events.length=0;s.spawnProjectile(a);const shot=[...s.projectiles.values()][0];let last=shot.pos;
 for(let n=0;n<400&&s.projectiles.has(shot.id);n++){last={...shot.pos};s.stepProjectiles(1/120);}
 const hit=s.events.find(e=>e.type==='impact')?.pos??last;s.dispose();
 // Distance from the crosshair ray (eye -> aim point) to the impact.
 const dir=forward(a.input.yaw,a.input.pitch),rel=sub(hit,eye),along=rel.x*dir.x+rel.y*dir.y+rel.z*dir.z;return len(sub(rel,mul(dir,along)));
}
test('ADS shots land on the crosshair ray at 10/25/50 m for every gun (sights zeroed to the aim point)',()=>{
 for(const spec of GUNS)for(const d of [10,25,50]){const miss=range(spec,d,true);assert.ok(miss<.08,`${spec} ADS at ${d} m misses the reticle ray by ${miss.toFixed(3)} m`);}
});
test('hip fire keeps its straight launch (gravity drop below the crosshair is unchanged outside ADS)',()=>{
 assert.ok(range('pistol',25,false)>1,'pistol hip fire still drops at 25 m');
});
