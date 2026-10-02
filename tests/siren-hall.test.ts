import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {sirenStageBlocks,SIREN_HALL,SIREN_ADD_SPAWNS,SIREN_CHIP_SPOTS} from '../src/shared/siren-stage-layout.js';
import {v} from '../src/shared/config.js';
await initPhysics();

/** A real Ward Zero fight: an authorised Siren host forces the boss hall, seven bots join. */
function hall(bots=7){
 const s=new Simulation({skipIntros:true});s.addPlayer('host','Siren','dev',null,'#ffffff',{preferredSpec:'siren'},true);
 s.configureLobby({botCount:bots,botDifficulty:'normal'});s.setReady('host',true);
 for(let i=0;i<3000&&s.phase!=='fight';i++){if(s.phase==='specialization')s.selectSpec('host','siren');s.step();}
 return s;
}
test('the Siren fight runs in the sealed hall and eight pilots fight without leaving it',()=>{
 const s=hall();try{
  assert.equal(s.options.map,'siren');assert.equal(s.phase,'fight');assert.equal(s.props.size,sirenStageBlocks().length);
  let top=0;
  for(let i=0;i<900;i++){s.step();for(const p of s.players.values()){const t=p.body.translation();top=Math.max(top,t.y);
   assert.ok([t.x,t.y,t.z].every(Number.isFinite));assert.ok(Math.abs(t.x)<SIREN_HALL.halfX&&Math.abs(t.z)<SIREN_HALL.halfZ&&t.y>-1&&t.y<SIREN_HALL.roof,`${p.id} left the hall at ${t.x.toFixed(1)},${t.y.toFixed(1)},${t.z.toFixed(1)}`);}}
  assert.ok([...s.players.values()].some(p=>p.bot&&p.lastAttack>0),'bots engage inside the hall');
  assert.ok(top<SIREN_HALL.roof);
 }finally{s.dispose();}
});
test('a launch at the lift cap hits the vault instead of escaping, and aimed ground effects land under the roof',()=>{
 const s=hall(1);try{
  const p=[...s.players.values()].find(q=>q.bot)!;p.body.setTranslation(v(2,1.1,2),true);p.body.setLinvel(v(0,24,0),true);p.ext.y=24;
  let top=0;for(let i=0;i<120;i++){s.step();top=Math.max(top,p.body.translation().y);}
  assert.ok(top<SIREN_HALL.roof,'the roof contains the launch');assert.ok(Math.abs(p.body.translation().x)<SIREN_HALL.halfX);
  // An Eruption aimed at the vault (the aim ray hits the roof) is placed on the floor, not on the roof top.
  const g=s.siren.ground(v(3,SIREN_HALL.roof+.4,2));assert.ok(g.y<.5,`landed at y=${g.y.toFixed(2)}`);
  for(const a of SIREN_ADD_SPAWNS){const at=s.siren.ground(v(a.x,1,a.z));assert.ok(Math.abs(at.y)<.05,`${a.id} is not on the floor`);}
  for(const c of SIREN_CHIP_SPOTS){const at=s.siren.ground(v(c.x,1,c.z));assert.ok(Math.abs(at.y)<.05,`${c.id} is not on the floor`);}
 }finally{s.dispose();}
});
