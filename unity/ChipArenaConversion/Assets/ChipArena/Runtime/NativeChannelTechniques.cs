using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private NativePilot ExpansionOwner(int id){foreach(var q in Pilots)if(q.Seat.id==id)return q;return null;}
        private bool ChannelLive(NativePilot p,string kind)=>p?.Technique?.Kind==kind&&NativeTechniqueRules.Live(p.Technique,Time)&&!p.Technique.Recast(Time);
        private bool ReleaseChannelTechnique(NativePilot p)
        {
            var k=p.Technique;
            switch(k.Kind)
            {
                case "bipod":k.Phase="planted";k.Point=Vec(p.Body.position-Vector3.up*.865f);k.Until=k.Release+(AbilityChip(p,"ab-belt-feed")?7:5);p.TechniqueNext=k.Release+.25f;return true;
                case "envenom":k.Phase="coated";k.Arm=3;return true;
                case "dragon-breath":k.Phase="breath";k.Arm=Time;k.Until=k.Release+(AbilityChip(p,"ab-deep-lungs")?2.4f:1.6f);break;
                case "soul-siphon":k.Phase="drain";k.Target=ThirdAimTarget(p,in p.Input,AbilityChip(p,"ab-soul-feast")?14:10);if(k.Target==0){EndTechnique(p);return true;}break;
                case "combust":Combust(p);return true;
                default:return false;
            }
            k.RecoverUntil=k.Until;p.Ready=k.RecoverUntil;p.TechniqueNext=k.Release+.1f;return true;
        }
        private void FinishChannelTechnique(NativePilot p)
        {
            var k=p.Technique;if(k==null||k.Recast(Time))return;k.RecastAt=Time;
            if(k.Kind=="bipod"){k.Phase="packup";k.Until=k.RecoverUntil=Time+.3f;p.Ready=k.RecoverUntil;}
            else {EndTechnique(p);k.RecastAt=Time;}
        }
        private bool StepChannelTechnique(NativePilot p)
        {
            var k=p.Technique;
            if(k.Kind=="envenom"||k.Kind=="combust")return true;
            if(k.Kind!="bipod"&&k.Kind!="dragon-breath"&&k.Kind!="soul-siphon")return false;
            if(k.Phase=="recovery"||k.Recast(Time))return true;
            if(TechniqueBlocked(p)){EndTechnique(p);return true;}
            if(k.Kind=="bipod")
            {
                k.Dir=GunRules.Direction(p.Input.Yaw,p.Input.Pitch);
                while(p.TechniqueNext<=Math.Min(Time,k.Until)){if(AbilityChip(p,"ab-belt-feed"))Heal(p,.75f);p.TechniqueNext+=.25f;}
                if(Time>=k.Until)FinishChannelTechnique(p);return true;
            }
            if(k.Kind=="dragon-breath")
            {
                k.Dir=Vec(Vector3.RotateTowards(Vec(k.Dir),Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)),120*Mathf.Deg2Rad/60,0));
                while(p.TechniqueNext<=Math.Min(Time,k.Until)+.0001f)
                {
                    p.TechniqueNext+=.1f;float cone=AbilityChip(p,"ab-deep-lungs")?40:30;
                    foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(p.Body.position,t.at)<=7&&Vector3.Angle(Vec(k.Dir),t.at-p.Body.position)<=cone*.5f&&MeleeVisible(p.Body.position,t.at,p,t.collider))
                    {if(TechniqueHit(p,t.id,1,t.at,Vec(k.Dir))<=0)continue;if(t.pilot!=null)RefreshDot(t.pilot,p,"fire",1.5f,2);else BurnMinion(t.minion,p,1.5f);}
                }
            }
            else
            {
                var t=MeleeTarget(p,k.Target);float reach=AbilityChip(p,"ab-soul-feast")?16:12;
                if(t.collider==null||Vector3.Distance(p.Body.position,t.at)>reach||!MeleeVisible(p.Body.position,t.at,p,t.collider)){FinishChannelTechnique(p);return true;}
                k.To=Vec(t.at);
                while(p.TechniqueNext<=Math.Min(Time,k.Until)+.0001f)
                {
                    p.TechniqueNext+=.1f;float actual=TechniqueHit(p,t.id,.8f,t.at,Vector3.zero);Heal(p,actual);
                    if(actual>0&&AbilityChip(p,"ab-soul-feast")&&t.pilot!=null)
                    {
                        var q=t.pilot;bool stamina=q.Weapon.@class=="melee";float taken=Math.Min(1,stamina?q.Stamina:q.Mana);
                        if(stamina){q.Stamina-=taken;p.Stamina=Math.Min((float)p.Stats["maxStamina"],p.Stamina+taken);}else{q.Mana-=taken;p.Mana=Math.Min((float)p.Stats["maxMana"],p.Mana+taken);}
                    }
                }
            }
            if(Time>=k.Until)FinishChannelTechnique(p);return true;
        }
        private void EnvenomHit(NativePilot p,int target,Vector3 at)
        {
            var k=p.Technique;if(!ChannelLive(p,"envenom")||k.Arm<1)return;var t=MeleeTarget(p,target);
            k.Arm--;k.HitAt=Time;k.To=Vec(at);if(k.Arm==0)k.Until=Time;if(t.collider==null)return;
            if(t.pilot!=null){RefreshDot(t.pilot,p,"poison",2,4);Slow(t.pilot,.85f,2);}
            else
            {
                string key=p.Seat.id+":poison";t.minion.Dots.TryGetValue(key,out var old);bool live=old!=null&&old.Until>=Time;
                t.minion.Dots[key]=new NativeDot{Source=p.Seat.id,Dps=Math.Max(2,live?old.Dps:0),Next=live?old.Next:Time+.25,Until=Time+4,HubNoReward=HubInside(p.Body.position)};SlowMinion(t.minion,.85f,2);
            }
            if(k.Arm==0)k.Until=Time;
        }
        private void Combust(NativePilot p)
        {
            var k=p.Technique;k.Phase="blow";k.Arm=Time;
            var blasts=new List<(Vector3 at,float damage)>();
            foreach(var t in MeleeTargets(p).ToArray())
            {
                if(Vector3.Distance(p.Body.position,t.at)>14||!MeleeVisible(p.Body.position,t.at,p,t.collider))continue;
                var dots=t.pilot?.Dots??t.minion.Dots;var burns=dots.Where(x=>(x.Key.EndsWith(":fire",StringComparison.Ordinal)||x.Key.EndsWith(":burn",StringComparison.Ordinal))&&x.Value.Until>Time).ToArray();if(burns.Length==0)continue;
                float remaining=0;foreach(var burn in burns){remaining+=(float)(burn.Value.Dps*Math.Max(0,burn.Value.Until-Time));dots.Remove(burn.Key);}blasts.Add((t.at,Math.Min(22,8+remaining)));
            }
            k.Path=blasts.Take(6).Select(x=>Vec(x.at)).ToArray();
            // Overlapping detonations never multiply the 22-damage base budget on one victim.
            foreach(var t in MeleeTargets(p).ToArray())
            {float damage=0;foreach(var b in blasts)if(Vector3.Distance(b.at,t.at)<=2&&MeleeVisible(b.at,t.at,p,t.collider))damage=Math.Max(damage,b.damage);if(damage>0)TechniqueHit(p,t.id,damage,t.at,Vector3.zero);}
        }
        private void ExpansionBlock(NativePilot p)
        {
            if(!Has(p,"wild-hush"))return;
            foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q)&&Vector3.Distance(p.Body.position,q.Body.position)<=6&&MeleeVisible(p.Body.position,q.Body.position,p,q.Collider))MarkTechnique(q,"wild-hush",p,1.5f);
        }
        private void StepExpansionWorldProcs(NativePilot p)
        {
            if(p.Hp<=0||!p.Seat.connected)return;
            if(Has(p,"wild-doomsday-clock"))
            {
                if(p.ExpansionStill<0||HubInside(p.Body.position))p.ExpansionDoomAt=-1;
                else if(p.ExpansionDoomAt<0)p.ExpansionDoomAt=Time;
                else if(Time-p.ExpansionDoomAt>=3){p.ExpansionDoomAt=Time;ChipProc(p,"wild-doomsday-clock",p.Body.position,0,60);ChipBlast(p,p.Body.position,7,60,0,effect:"wild-doomsday-clock");Heal(p,20);}
            }
            if(!Has(p,"wild-bodyguard-drone")||Time<p.ExpansionDroneAt)return;
            NativeBullet chosen=null;float distance=36;
            foreach(var shot in Bullets){var owner=ExpansionOwner(shot.Owner);float d=(shot.Position-p.Body.position).sqrMagnitude;if(shot.Life>0&&d<=distance&&owner!=null&&Enemy(owner,p)){distance=d;chosen=shot;}}
            NativeAbilityShot ability=null;
            foreach(var shot in AbilityShots){float d=(shot.Position-p.Body.position).sqrMagnitude;if(shot.Life>0&&d<=distance&&shot.Owner!=null&&Enemy(shot.Owner,p)){distance=d;ability=shot;}}
            PveAcidState acid=null;Vector3 acidPoint=Vector3.zero;
            foreach(var spit in pveAcid)
            {
                var r=spit.Record;if(spit.Landed||Time>=r.at+r.flight||p.Seat.AuthorityBoss||r.devMonster&&r.devTarget!=0&&r.devTarget!=p.Seat.id)continue;
                NativeHordeFx.Glob(new System.Numerics.Vector3(r.from.x,r.from.y,r.from.z),new System.Numerics.Vector3(r.to.x,r.to.y,r.to.z),r.flight,Time-r.at,out var glob);
                var at=new Vector3(glob.X,glob.Y,glob.Z);float d=(at-p.Body.position).sqrMagnitude;if(d<=distance){distance=d;acid=spit;acidPoint=at;}
            }
            if(acid!=null){pveAcid.Remove(acid);p.ExpansionDroneAt=Time+3;ChipProc(p,"wild-bodyguard-drone",acidPoint,acid.Record.actor,acid.Record.id,from:p.Body.position);}
            else if(ability!=null){AbilityShots.Remove(ability);p.ExpansionDroneAt=Time+3;ChipProc(p,"wild-bodyguard-drone",ability.Position,0,0,from:p.Body.position,projectile:ability.Id);}
            else if(chosen!=null){EndBullet(chosen);p.ExpansionDroneAt=Time+3;ChipProc(p,"wild-bodyguard-drone",chosen.Position,0,0,from:p.Body.position,projectile:chosen.Id);}
        }
    }
}
