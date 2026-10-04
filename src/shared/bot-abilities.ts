import type {Player,Simulation} from './simulation.js';
import type {Input,Vec} from './config.js';
import {WEAPONS,abilityUser} from './content.js';
import {ARCADE} from './arcade-content.js';
import {SIREN_SLOTS} from './siren-contract.js';
import {committed,rolling} from './souls-combat.js';

/** Choose normal player inputs. The combat systems still own costs, cooldowns and hits. */
/** Live kits aim and lead with `target`/`targetVel`. */
export function botAbilities(s:Simulation,p:Player,input:Input,distance:number,visible:boolean,difficulty:number,target?:Vec,targetVel?:Vec){
 input.secondary=false;input.ability=false;
 if(p.bot==='dummy'||s.phase!=='fight'||p.hp<=0||!p.connected||input.block||p.blockTime>0||s.arcade.stunned(p)||s.leet.frozen(p)||s.siren.heldVictim(p)||rolling(p,s.time)||committed(p,s.time))return;
 if(s.arcade.active(p)&&p.arcade?.action==='charge'){
  input.hadoken=visible&&s.time-p.arcade.at<ARCADE.chargeSeconds+.05&&p.stamina>=30*p.stats.resourceCost;
  input.attack=input.dash=false;return;
 }
 // Live ability kits (§3.14) never fall into the legacy rows below.
 if(abilityUser(p.spec)&&s.abilities.botKit(p.spec)){
  s.abilities.botAds(p,input,distance,visible);
  const cast=s.abilities.botCast(p,input,distance,visible,difficulty,target,targetVel);
  if(!cast&&(p.cooldown>0||p.reloadTime>0||p.cast||p.burst>0))input.attack=false;
  return;
 }
 // Release primary during recovery so a held attack cannot consume the first ready
 // frame before the next ability decision (cooldowns tick after bot input).
 if(p.cooldown>0||p.reloadTime>0||p.swing||p.cast||p.burst>0||s.standardMelee.busy(p)||s.siren.busy(p)){input.attack=false;return;}
 const w=WEAPONS[p.spec],afford=(cost:number)=>(w.class==='magic'?p.mana:p.stamina)>=cost*p.stats.resourceCost;
 if(p.spec==='siren'&&s.siren.active(p)){
  const state=s.siren.state(p),ranged=state.mode==='ranged';
  if((!ranged&&distance>8||ranged&&distance<3.5)&&!p.input.sirenToggle){input.sirenToggle=true;input.attack=input.dash=false;return;}
  if(!visible||s.random()>[.4,.65,.85][difficulty])return;
  const slots=SIREN_SLOTS[state.mode].abilities;
  const candidates=([1,2,3,4] as const).filter(slot=>{
   const move=slots[slot-1];if((state.ready?.[move]||0)>s.time)return false;
   if(move==='mark'&&s.siren.marks.some(m=>m.owner===p.id))return false;
   return ranged?distance<=28:distance<=(slot===1?3*(state.boost?.reach??1):slot===2?3.6*(state.boost?.reach??1):slot===3?6*(state.boost?.reach??1):7);
  });
  if(candidates.length){input.sirenAbility=candidates[Math.floor(s.random()*candidates.length)];input.attack=input.dash=false;}
  return;
 }
 if(!visible)return;
 if(s.emperor.active(p)&&distance>3.3&&distance<11){input.secondary=true;input.attack=input.dash=false;return;}
 if(s.random()>[.4,.65,.85][difficulty])return;
 if(s.emperor.active(p)){
  if(distance<=2.7*p.stats.reach){input.electric=true;input.attack=input.dash=false;}return;
 }
 if(s.arcade.active(p)){
  const state=s.arcade.state(p),choices:('hadoken'|'uppercut'|'secondary')[]=[];
  if(distance>=4&&distance<=25&&state.hadokenCd<=0&&afford(12))choices.push('hadoken');
  if(distance<=3&&state.uppercutCd<=0&&afford(25))choices.push('uppercut');
  if(distance<=4.5&&p.secondaryCd<=0&&afford(22))choices.push('secondary');
  if(choices.length){input[choices[Math.floor(s.random()*choices.length)]]=true;input.attack=input.dash=false;}return;
 }
 if(p.secondaryCd>0)return;
 const range=p.chips.necronomicon&&w.class==='magic'?24:
  ({'sword-launch':w.range*p.stats.reach+.25,'dagger-throw':22,'shield-throw':20,'wolf-spin':5,root:p.chips['l44t-time']?28:5.8} as Record<string,number>)[w.secondary]??Math.min(40,w.range);
 const cost=w.class==='magic'?(w.secondary==='root'?30:p.chips.necronomicon?35:20):(w.secondaryCost||0);
 if(distance<=range&&afford(cost)&&(p.spec!=='greatsword'||p.grounded)){
  input.secondary=true;input.attack=input.dash=false;
 }
}
