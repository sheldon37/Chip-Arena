import type {Chip,Stats} from './content.js';
const labels:Record<keyof Stats,string>={blockCooldown:'Block cooldown',blockWindow:'Block window (seconds)',reflectDamage:'Reflected damage',reflectSpeed:'Reflected speed',blockPulseDamage:'Counter Pulse base damage',blockPulseImpulse:'Counter Pulse base impulse',hp:'Maximum health',speed:'Movement speed',attackRate:'Attack rate',damage:'Damage per hit',damageTaken:'Incoming damage',knockbackTaken:'Received knockback',dashCooldown:'Dash cooldown',bounces:'Projectile bounces',split:'Splitter level',pierces:'Target pierces',burn:'Burn damage / second',poison:'Poison damage / second',staticEvery:'Direct hits per discharge',staticDamage:'Discharge damage',reach:'Base melee reach',impulse:'Applied impulse',demolition:'Base structural damage',shrapnel:'Explosion damage',debrisTaken:'Debris damage',extraProjectiles:'Extra projectiles',extraStrikes:'Follow-up sweeps',ammoCapacity:'Base magazine capacity',maxMana:'Maximum mana',maxStamina:'Maximum stamina',projectileSpeed:'Projectile speed',homing:'Homing degrees / second'};
const decimal=(n:number)=>Number(n.toFixed(3)).toString();
/** Uses the same declarative operations and per-Chip caps as statsFor. */
export function chipStackEffects(chip:Chip,copies:number){const count=Math.max(0,Math.min(chip.max,Math.floor(copies)));if(!count)return [];
 return chip.effects.map(effect=>{let value:string;
  if(effect.op==='mul')value=`×${decimal(effect.value**count)}`;
  else if(effect.op==='set')value=decimal(effect.value);
  else {const amount=effect.value*count;value=['reach','demolition','ammoCapacity'].includes(effect.stat)?`+${decimal(amount*100)}%`:`${amount>=0?'+':''}${decimal(amount)}`;}
  return {label:labels[effect.stat],value};
 });
}
