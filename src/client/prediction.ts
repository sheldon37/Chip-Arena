import {RAPIER,Snapshot,PlayerView} from '../shared/simulation.js';
import {Vec,Input,CONFIG as C,MATERIALS,v,add,sub,mul,len} from '../shared/config.js';
import {statsFor} from '../shared/content.js';
import {prepareMotion,finishMotion,Motion} from '../shared/movement.js';
import {NETWORK} from '../shared/network-config.js';
/** Replays the authority's dynamic capsule and locomotion. Damage remains authoritative. */
export class Predictor {
 world=new RAPIER.World(v(0,C.gravity,0));body:RAPIER.RigidBody;collider:RAPIER.Collider;
 props=new Map<string,RAPIER.RigidBody>();pending:{input:Input;dt:number}[]=[];
 pos=v();velocity=v();move=v();external=v();dashTime=0;dash=0;error=0;phase='';initialized=false;
 private offset=v();private epoch='';private grounded=false;
 constructor(){this.world.timestep=1/C.simulationHz;this.world.maxCcdSubsteps=4;this.body=this.world.createRigidBody(RAPIER.RigidBodyDesc.dynamic().lockRotations().setCcdEnabled(true).setLinearDamping(0).setCanSleep(false));this.collider=this.world.createCollider(RAPIER.ColliderDesc.capsule(C.playerHalfHeight,C.playerRadius).setMass(C.playerMass).setFriction(0).setRestitution(0),this.body);}
 reconcile(s:Snapshot,me:PlayerView){
  const ids=new Set(s.props.map(p=>p.id));for(const[id,b]of this.props)if(!ids.has(id)){this.world.removeRigidBody(b);this.props.delete(id);}
  for(const p of s.props){let b=this.props.get(p.id);if(!b){b=this.world.createRigidBody(RAPIER.RigidBodyDesc.fixed());this.world.createCollider(RAPIER.ColliderDesc.cuboid(p.size.x/2,p.size.y/2,p.size.z/2).setFriction(MATERIALS[p.material].friction).setRestitution(.12),b);this.props.set(p.id,b);}b.setTranslation(p.pos,false);b.setRotation(p.rot,false);}
  const previous={...this.pos},old=add(this.pos,this.offset),epoch=`${s.generation||0}:${s.setNumber}:${s.round}:${s.phase}:${me.hp>0}`,reset=this.epoch!==epoch||!this.initialized;
  this.pos={...me.pos};this.velocity={...me.vel};this.move=me.move?{...me.move}:v(me.vel.x,0,me.vel.z);this.external=me.external?{...me.external}:v();this.dashTime=me.dashTime||0;this.dash=me.dashCd;this.grounded=me.grounded;
  this.body.setTranslation(this.pos,true);this.body.setLinvel(this.velocity,true);this.world.propagateModifiedBodyPositionsToColliders();
  this.pending=this.pending.filter(p=>p.input.seq>me.ack);if(reset)this.pending=[];
  this.phase=s.phase;this.epoch=epoch;this.initialized=true;
  for(const p of this.pending)this.integrate(p.input,p.dt,me);
  this.error=len(sub(previous,this.pos));
  // Rendering only: simulation/collisions use the corrected position immediately.
  // Large errors, death and round teleports never ease across the arena.
  this.offset=!reset&&this.error<NETWORK.hardSnapDistance?sub(old,this.pos):v();
 }
 step(input:Input,dt:number,me:PlayerView){this.pending.push({input:{...input},dt});if(this.pending.length>NETWORK.maxPendingInputs)this.pending.shift();this.integrate(input,dt,me);}
 integrate(input:Input,dt:number,me:PlayerView){
  const stats=statsFor(me.chips),motion:Motion={move:this.move,ext:this.external,dashCd:this.dash,dashTime:this.dashTime,grounded:this.grounded};
  prepareMotion(this.world,this.body,this.collider,motion,input,stats,dt,me.guarding,me.swinging);
  const before={...this.body.linvel()};this.world.timestep=dt;this.world.step();finishMotion(motion,before,this.body.linvel(),stats.knockbackTaken,dt);
  this.pos={...this.body.translation()};this.velocity={...this.body.linvel()};this.move=motion.move;this.external=motion.ext;this.dash=motion.dashCd;this.dashTime=motion.dashTime;this.grounded=motion.grounded;
 }
 renderPosition(dt:number){this.offset=mul(this.offset,Math.exp(-NETWORK.correctionDecay*dt));if(len(this.offset)<.001)this.offset=v();return add(this.pos,this.offset);}
 get visualOffset(){return len(this.offset);}
 dispose(){this.world.free();}
}
