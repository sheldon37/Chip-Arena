import {attackCommitment,attackStep,committed,rolling,rollProfile,usesRoll,type RollState,type AttackCommitment} from '../shared/souls-combat.js';
import {meleeMotionInput,MELEE_SPECIALS,normalMelee} from '../shared/standard-melee.js';
import {recoveryMotion,type Knockdown} from '../shared/recovery.js';
import {EMPEROR} from '../shared/emperor-content.js';
import {canUse} from '../shared/access.js';
import {DEMON_STEP,demonStepUser} from '../shared/demon-step.js';
import {RAPIER,Snapshot,PlayerView} from '../shared/simulation.js';
import {Vec,Input,CONFIG as C,MATERIALS,v,add,sub,mul,len,forward,norm} from '../shared/config.js';
import {statsFor,WEAPONS} from '../shared/content.js';
import {prepareMotion,finishMotion,Motion} from '../shared/movement.js';
import {NETWORK} from '../shared/network-config.js';
/** Replays the authority's dynamic capsule and locomotion. Damage remains authoritative. */
export class Predictor {
 world=new RAPIER.World(v(0,C.gravity,0));body:RAPIER.RigidBody;collider:RAPIER.Collider;
 props=new Map<string,RAPIER.RigidBody>();pending:{input:Input;dt:number}[]=[];
 pos=v();velocity=v();move=v();external=v();dashTime=0;waveTime=0;electricStepTime=0;private electricCooldown=0;dash=0;error=0;phase='';initialized=false;
 roll?:RollState;attackCommitment?:AttackCommitment;private rollHeld=false;private stamina=100;private staminaAt=-10;private attackCooldown=0;private secondaryCooldown=0;private map:unknown;private recovery?:Knockdown;private time=0;
 private groundHint:boolean|undefined;private stunRemaining=0;private offset=v();private epoch='';private grounded=false;
 constructor(){this.world.timestep=1/C.simulationHz;this.world.maxCcdSubsteps=4;this.body=this.world.createRigidBody(RAPIER.RigidBodyDesc.dynamic().lockRotations().setCcdEnabled(true).setLinearDamping(0).setCanSleep(false));this.collider=this.world.createCollider(RAPIER.ColliderDesc.capsule(C.playerHalfHeight,C.playerRadius).setMass(C.playerMass).setFriction(0).setFrictionCombineRule(RAPIER.CoefficientCombineRule.Min).setRestitution(0),this.body);}
 reconcile(s:Snapshot,me:PlayerView){
  const ids=new Set(s.props.map(p=>p.id));for(const[id,b]of this.props)if(!ids.has(id)){this.world.removeRigidBody(b);this.props.delete(id);}
  for(const p of s.props){let b=this.props.get(p.id);if(!b){b=this.world.createRigidBody(RAPIER.RigidBodyDesc.fixed());this.world.createCollider(RAPIER.ColliderDesc.cuboid(p.size.x/2,p.size.y/2,p.size.z/2).setFriction(MATERIALS[p.material].friction).setRestitution(.12),b);this.props.set(p.id,b);}b.setTranslation(p.pos,false);b.setRotation(p.rot,false);}
  const previous={...this.pos},old=add(this.pos,this.offset),epoch=`${s.generation||0}:${s.setNumber}:${s.round}:${s.phase}:${me.hp>0}:${me.leet?.controlling||''}:${me.leet?.controlledBy||''}:${!!me.leet?.scare}:${(me.leet?.frozenUntil||0)>s.time}:${(me.leet?.launchUntil||0)>s.time}`,reset=this.epoch!==epoch||!this.initialized;
  this.map=s.options.map;this.time=s.time;this.roll=me.roll?{...me.roll,direction:{...me.roll.direction}}:undefined;this.rollHeld=!!me.rollHeld;this.stamina=me.stamina;this.staminaAt=me.staminaAt??s.time;this.attackCommitment=me.attackCommitment?{...me.attackCommitment}:undefined;this.attackCooldown=me.cooldown;this.secondaryCooldown=me.secondaryCd;this.recovery=me.knockdown?{...me.knockdown}:undefined;this.pos={...me.pos};this.velocity={...me.vel};this.move=me.move?{...me.move}:v(me.vel.x,0,me.vel.z);this.external=me.external?{...me.external}:v();this.dashTime=me.dashTime||0;this.waveTime=me.waveTime||0;this.electricStepTime=me.electricStepTime||0;this.electricCooldown=me.cooldown;this.dash=me.dashCd;this.grounded=me.grounded;this.groundHint=me.grounded;this.stunRemaining=Math.max(0,Math.max(me.hitStunUntil||0,me.knockdown?.until||0,me.juggle?.until||0)-s.time);
  this.body.setTranslation(this.pos,true);this.body.setLinvel(this.velocity,true);this.world.propagateModifiedBodyPositionsToColliders();
  this.pending=this.pending.filter(p=>p.input.seq>me.ack);if(reset||me.leet?.controlling||me.leet?.controlledBy||me.leet?.scare||(me.leet?.frozenUntil||0)>s.time||(me.leet?.launchUntil||0)>s.time)this.pending=[];
  this.phase=s.phase;this.epoch=epoch;this.initialized=true;
  for(const p of this.pending)this.integrate(p.input,p.dt,me);
  this.error=len(sub(previous,this.pos));
  // Rendering only: simulation/collisions use the corrected position immediately.
  // Large errors, death and round teleports never ease across the arena.
  this.offset=!reset&&this.error<NETWORK.hardSnapDistance?sub(old,this.pos):v();
 }
 step(input:Input,dt:number,me:PlayerView){this.pending.push({input:{...input},dt});if(this.pending.length>NETWORK.maxPendingInputs)this.pending.shift();this.integrate(input,dt,me);}
 integrate(input:Input,dt:number,me:PlayerView){
  this.time+=dt;const stats=statsFor(me.chips),emperor=me.spec==='emperor'&&canUse(me,'weapon:emperor');
  if(this.stunRemaining>0){this.roll=undefined;this.attackCommitment=undefined;}
  const requested=input.dash||(emperor&&input.secondary),pressed=requested&&!this.rollHeld;this.rollHeld=requested;
  const roll=rollProfile(me.spec,stats),grounded=this.groundHint??this.grounded,free=this.stunRemaining<=0&&me.hp>0&&me.blockTime<=0&&!me.guarding&&!committed(this,this.time)&&!rolling(this,this.time);
  if(usesRoll(me.spec)&&pressed&&free&&grounded&&this.dash<=0&&!me.swinging&&!me.castAt&&me.reloadTime<=0&&!(me.meleeAction&&this.time<me.meleeAction.until)&&(!emperor||this.attackCooldown<=0)&&this.stamina>=roll.cost){
   const direction=Math.hypot(input.x,input.z)>.1?norm(v(input.x,0,input.z)):forward(input.yaw);
   this.roll={at:this.time,until:this.time+roll.duration,invulnerableAt:this.time+roll.iframeStart,invulnerableUntil:this.time+roll.iframeStart+roll.iframeSeconds,direction,distance:roll.distance};this.stamina-=roll.cost;this.staminaAt=this.roll.until;this.dash=roll.cooldown;this.move=v();this.electricStepTime=0;
  }
  this.attackCooldown=Math.max(0,this.attackCooldown-dt);this.secondaryCooldown=Math.max(0,this.secondaryCooldown-dt);
  if(this.time-this.staminaAt>=C.staminaRegenDelay)this.stamina=Math.min(stats.maxStamina,this.stamina+dt*C.staminaRegen*stats.staminaRegen);
  if(normalMelee(me.spec)&&free&&!rolling(this,this.time)&&this.attackCooldown<=0&&me.reloadTime<=0&&!(me.meleeAction&&this.time<me.meleeAction.until)){
   const w=WEAPONS[me.spec],special=input.secondary&&this.secondaryCooldown<=0&&(me.spec!=='greatsword'||grounded),cost=(special?(w.secondaryCost||0):w.cost+stats.extraStrikes*C.followupCost)*stats.resourceCost;
   if((special||input.attack)&&this.stamina>=cost){
    if(special){const info=MELEE_SPECIALS[me.spec as keyof typeof MELEE_SPECIALS],until=this.time+(me.spec==='greatsword'?3.5:(info.windup+info.recover)/stats.attackRate);this.attackCommitment={at:this.time,until,yaw:input.yaw,stepAt:this.time,stepUntil:this.time,distance:0};this.secondaryCooldown=w.secondaryCooldown;}
    else {this.attackCommitment=attackCommitment(me.spec,this.time,w.windup/stats.attackRate,.16/stats.attackRate,stats.attackRate,Math.max(w.interval,w.windup+.16+stats.extraStrikes*(C.followupGap+.16)+.12)/stats.attackRate);this.attackCommitment.yaw=input.yaw;}
    this.stamina-=cost;this.staminaAt=this.attackCommitment.until;this.attackCooldown=this.attackCommitment.until-this.time;this.move=v();
   }
  }
  this.electricCooldown=Math.max(0,this.electricCooldown-dt);if(emperor&&!rolling(this,this.time)&&input.electric&&this.electricCooldown<=0&&me.blockTime<=0&&this.stunRemaining<=0){this.electricStepTime=(this.groundHint??this.grounded)?EMPEROR.electricStepSeconds:0;this.electricCooldown=EMPEROR.electricRecovery/(stats.attackRate*(me.chips['mishima-godfist']?1.4:1));}else if(emperor&&!rolling(this,this.time)&&input.attack&&this.electricCooldown<=0&&me.blockTime<=0&&this.stunRemaining<=0){this.electricCooldown=(.075+(me.chips['mishima-pressure']?2:1)*.17+.14)/stats.attackRate;}const motion:Motion={electricStepTime:this.electricStepTime,move:this.move,ext:this.external,dashCd:this.dash,dashTime:this.dashTime,waveTime:this.waveTime,grounded:this.grounded};
  prepareMotion(this.world,this.body,this.collider,motion,meleeMotionInput(usesRoll(me.spec)?{...input,dash:false}:input,me.meleeAction,this.time),{...stats,time:this.time,roll:this.roll,locomotionLock:committed(this,this.time),attackStep:attackStep(this.attackCommitment,this.time,dt),map:this.map,recoveryRoll:recoveryMotion(this.recovery,this.time,input),dashSpeed:demonStepUser(me)?DEMON_STEP.speed:undefined,dashDuration:demonStepUser(me)?DEMON_STEP.seconds:undefined,groundedHint:this.groundHint,electricStepSpeed:emperor?EMPEROR.electricStepSpeed:undefined,waveSpeed:undefined,speed:stats.speed*(me.motionScale??1)},dt,me.guarding,me.swinging,this.stunRemaining>0);this.groundHint=undefined;this.stunRemaining=Math.max(0,this.stunRemaining-dt);
  const before={...this.body.linvel()};this.world.timestep=dt;this.world.step();finishMotion(motion,before,this.body.linvel(),stats.knockbackTaken,dt);
  this.pos={...this.body.translation()};this.velocity={...this.body.linvel()};this.move=motion.move;this.external=motion.ext;this.dash=motion.dashCd;this.dashTime=motion.dashTime;this.waveTime=motion.waveTime||0;this.electricStepTime=motion.electricStepTime||0;this.grounded=motion.grounded;
 }
 renderPosition(dt:number){this.offset=mul(this.offset,Math.exp(-NETWORK.correctionDecay*dt));if(len(this.offset)<.001)this.offset=v();return add(this.pos,this.offset);}
 get visualOffset(){return len(this.offset);}
 dispose(){this.world.free();}
}
