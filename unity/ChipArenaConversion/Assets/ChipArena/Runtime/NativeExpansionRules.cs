using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {
        internal ChipStats ExpansionStats;
        internal float ExpansionRate=1,ExpansionSpeed=1,ExpansionStill=-1,ExpansionLastHit=-100,ExpansionThiefAt=-100,ExpansionDroneAt=-100,ExpansionDoomAt=-1;
    }
    public sealed partial class NativeArena
    {
        private float ExpansionDamage(NativePilot p,float damage,bool hit=true)
        {
            if(p==null)return damage;if(hit&&Has(p,"wild-haymaker")&&Time>=p.Wild.HayAt)damage*=6;
            if(p.Hp>.9f*p.Stats["hp"])damage*=1+.2f*ExpansionStacks(p,"wild-top-shape");
            if(!p.Grounded)damage*=1+.25f*ExpansionStacks(p,"wild-hangtime");
            if(p.ExpansionStill>=0&&Time-p.ExpansionStill>=1)damage*=1+.25f*ExpansionStacks(p,"wild-planted");
            if(hit&&Time-p.ExpansionLastHit>=4)damage*=1+.5f*ExpansionStacks(p,"wild-opening-move");return damage;
        }
        private void ExpansionHit(NativePilot p,int target,Vector3 at,float actual,bool melee,Vector3 direction=default,bool projectile=false)
        {
            if(p==null||actual<=0)return;WildHit(p,target,at,actual,direction,projectile);p.ExpansionLastHit=Time;EnvenomHit(p,target,at);
            var victim=Pilots.Find(q=>q.Seat.id==target);if(victim!=null&&Has(p,"wild-time-thief")&&Time>=p.ExpansionThiefAt){p.ExpansionThiefAt=Time+.25f;p.QReady=Math.Max(Time,p.QReady-.25f);p.EReady=Math.Max(Time,p.EReady-.25f);victim.QReady=Math.Max(Time,victim.QReady)+.25f;victim.EReady=Math.Max(Time,victim.EReady)+.25f;ChipProc(p,"wild-time-thief",at,target,.25f,from:p.Body.position);}
            int mana=ExpansionStacks(p,"wild-mana-tap"),stamina=melee?ExpansionStacks(p,"wild-follow-through"):0;
            if(mana>0)p.Mana=Math.Min((float)p.Stats["maxMana"],p.Mana+4*mana);
            if(stamina>0)p.Stamina=Math.Min((float)p.Stats["maxStamina"],p.Stamina+8*stamina);
            if(Has(p,"wild-hand-to-mouth")&&p.Weapon.ammo>0&&p.Ammo<Magazine(p)){p.Ammo++;ChipProc(p,"wild-hand-to-mouth",at,target,1);}
        }
        private int ThirdAimTarget(NativePilot p,in InputFrame aim,float range,bool burning=false,bool bossAllowed=true)
        {
            Vector3 from=p.Body.position,dir=Vec(GunRules.Direction(aim.Yaw,aim.Pitch));int chosen=0;float best=float.MaxValue;
            foreach(var t in MeleeTargets(p))
            {
                var delta=t.at-from;if(delta.magnitude>range||!MeleeVisible(from,t.at,p,t.collider)||!bossAllowed&&(t.pilot?.Seat.AuthorityBoss==true||t.minion!=null&&IsZoneBoss(t.minion)))continue;
                if(burning&&!(t.pilot?.Dots??t.minion?.Dots).Any(x=>(x.Key.EndsWith(":burn",StringComparison.Ordinal)||x.Key.EndsWith(":fire",StringComparison.Ordinal))&&x.Value.Until>Time))continue;
                float dot=Vector3.Dot(dir,delta.normalized);if(dot<.3f&&!burning)continue;float rank=1-dot+delta.magnitude*.001f;if(rank<best){best=rank;chosen=t.id;}
            }return chosen;
        }
        private void StepExpansionPilot(NativePilot p)
        {
            if(p.Body.linearVelocity.magnitude>=.5f)p.ExpansionStill=-1;else if(p.ExpansionStill<0)p.ExpansionStill=Time;
            if(!ReferenceEquals(p.ExpansionStats,p.Stats)){p.ExpansionStats=p.Stats;p.ExpansionRate=p.ExpansionSpeed=1;}
            float rate=1,speed=1;StepWildPilot(p,ref rate,ref speed);var k=p.Technique;
            if(NativeTechniqueRules.Live(k,Time))
            {if(k.Kind=="gun-spin")rate*=1.25f;if(k.Kind=="overload"){rate*=1.25f;speed*=1.15f;}}
            if(ChannelLive(p,"bipod"))rate*=1.3f;if(NativeCombatMarks.Has(p.TechniqueMarks,"ammo-crate-speed",Time))rate*=1.2f;
            if(p.Hp<.35f*p.Stats["hp"])rate*=1+.25f*ExpansionStacks(p,"wild-cornered");
            if(Time-p.ExpansionLastHit<2)speed*=1+.2f*ExpansionStacks(p,"wild-frenzy-scent");
            if(p.ExpansionRate!=rate){p.Stats["attackRate"]=p.Stats["attackRate"]/p.ExpansionRate*rate;p.ExpansionRate=rate;}
            if(p.ExpansionSpeed!=speed){p.Stats["speed"]=p.Stats["speed"]/p.ExpansionSpeed*speed;p.ExpansionSpeed=speed;}
            StepExpansionWorldProcs(p);
            bool planted=Has(p,"wild-planted"),opening=Has(p,"wild-opening-move"),doom=Has(p,"wild-doomsday-clock");int count=(planted?1:0)+(opening?1:0)+(doom?1:0),index=0;
            if(p.ChipGauges.Length!=count)p.ChipGauges=count==0?Array.Empty<NativeChipGauge>():new NativeChipGauge[count];
            if(planted)p.ChipGauges[index++]=new NativeChipGauge{Kind="wild-planted",Value=p.ExpansionStill<0?0:Mathf.Clamp01(Time-p.ExpansionStill)};
            if(opening)p.ChipGauges[index++]=new NativeChipGauge{Kind="wild-opening-move",Value=Time-p.ExpansionLastHit>=4?1:0};
            if(doom)p.ChipGauges[index]=new NativeChipGauge{Kind="wild-doomsday-clock",Value=p.ExpansionDoomAt<0?0:Mathf.Clamp01((Time-p.ExpansionDoomAt)/3)};
            bool expired=false;foreach(var mark in p.TechniqueMarks)if(mark.Until<=Time){expired=true;break;}
            if(expired)p.TechniqueMarks=p.TechniqueMarks.Where(x=>x.Until>Time).ToArray();WildGauges(p);
        }
        private bool ReleaseThirdTechnique(NativePilot p)
        {
            var k=p.Technique;if(ReleaseFinalTechnique(p))return true;if(ReleasePriorityTechnique(p))return true;if(ReleaseMobilityTechnique(p))return true;if(ReleaseChannelTechnique(p))return true;
            switch(k.Kind)
            {
                case "gun-spin":p.Ammo=Magazine(p);p.ReloadEnd=0;k.Arm=Time;k.Phase="hot";return true;
                case "overload":k.Arm=Time;k.Phase="live";return true;
                case "thunderclap":
                    k.Phase="clap";k.Arm=k.HitAt=Time;k.Point=Vec(p.Body.position-Vector3.up*.865f);
                    float radius=AbilityChip(p,"ab-sonic-boom")?7:5,stun=AbilityChip(p,"ab-sonic-boom")?.7f:.5f;
                    foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,p.Body.position)<=radius&&MeleeVisible(p.Body.position,t.at,p,t.collider)&&TechniqueHit(p,t.id,10,t.at,(t.at-p.Body.position).normalized)>0)
                    {if(t.pilot!=null){StunMelee(t.pilot,stun);CancelAbilities(t.pilot);CancelSpecial(t.pilot);EndTechnique(t.pilot);}else{SlowMinion(t.minion,.01f,stun);t.minion.NextAttack=Math.Max(t.minion.NextAttack,Time+stun);t.minion.StrikeAt=-1;if(t.minion.Pve!=null){t.minion.Pve.StaggerAt=Time+stun;t.minion.Pve.Struck=true;}}}
                    return true;
            }return false;
        }
        private bool StepThirdTechnique(NativePilot p)
        {
            var k=p.Technique;if(StepFinalTechnique(p))return true;if(StepPriorityTechnique(p))return true;if(StepMobilityTechnique(p))return true;if(StepChannelTechnique(p))return true;if(k.Kind=="gun-spin"||k.Kind=="thunderclap")return true;
            if(k.Kind!="overload")return false;
            if(Time>=k.Until&&k.Phase!="shock")
            {k.Phase="shock";if(k.Until-k.Release>=1){k.HitAt=Time;Damage(p,p,Math.Min(8,Math.Max(0,p.Hp-1)),Vector3.zero,0,false,forced:true,direct:false,primary:false,selfDamage:true,damageKind:"ability");}}
            return true;
        }
        private bool AbilitiesDisarmed(NativePilot p)=>p!=null&&(NativeCombatMarks.Has(p.TechniqueMarks,"disarm",Time)||NativeCombatMarks.Has(p.TechniqueMarks,"wild-hush",Time));
        private int ExpansionStacks(NativePilot p,string id)=>p!=null&&p.Seat.chips.TryGetValue(id,out int n)?Math.Max(0,n):0;
        private bool AbilityChip(NativePilot p,string id)=>Has(p,id)&&NativeAbilityChipRules.Slotted(Content,p.Seat.spec,p.Seat.bot?"":p.Seat.weaponTechniques,id);
        private void ChipProc(NativePilot p,string id,Vector3 at,int target=0,float value=0,Vector3? from=null,Vector3? direction=null,int projectile=0)=>Emit(EventKind.ChipProc,p,target,at,value,id,from:from,normal:direction,projectile:projectile);
        private void MarkTechnique(NativePilot p,string kind,NativePilot by,float seconds){p.TechniqueMarks=NativeCombatMarks.Put(p.TechniqueMarks,kind,by.Seat.id,Time,Time+seconds);}
    }
}
