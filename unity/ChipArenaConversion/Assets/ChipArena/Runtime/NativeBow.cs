using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot {public float BowDrawAt=-1,BowReleaseAt=-100,BowDraw,BowRainAt=-100;public Vector3 BowRainTarget;public int BowRainNext;}
    public sealed class NativeBowShot
    {public float Draw,Damage,Speed,Gravity,Spread,Impulse,Yaw;public string Detail;public Vector3? Origin,Direction;}
    public sealed partial class NativeArena
    {
        private void BotBow(NativePilot p,ref InputFrame input,Vector3 target,Vector3 velocity,float distance,bool visible)
        {
            input.Sprint=input.Reload=input.Aim=false;float draw=distance>15?.7f:distance<6?0:.35f;
            float hold=draw==0?.05f:(.25f+.55f*draw)/(float)p.Stats["attackRate"];
            input.Shoot=visible&&Time>=p.Ready&&(p.BowDrawAt<0||Time-p.BowDrawAt<hold);
            var delta=target-p.Body.position-Vector3.up*.3f;input.Pitch=GunRules.BallisticPitch(new Vector2(delta.x,delta.z).magnitude,delta.y,NativeBowRules.Speed(draw)*(float)p.Stats["projectileSpeed"],NativeBowRules.Gravity(draw)*(float)p.Stats["projectileGravity"]);
            if(!visible||p.BowDrawAt>=0)return;
            input.AbilityQ=distance>=6&&distance<=15&&Time>=p.QReady;
            input.AbilityE=!input.AbilityQ&&distance<=40&&Time>=p.EReady&&(velocity.sqrMagnitude<1||Pilots.Count(q=>q.Hp>0&&CanHit(p,q)&&Vector3.Distance(q.Body.position,target)<=3.5f)>=2);
            if(input.AbilityQ||input.AbilityE)input.Shoot=false;
        }
        private Vector3 BowRainOrigin(NativePilot p,Vector3 ground)
        {
            float radius=(float)((p.Weapon.shotRadius>0?p.Weapon.shotRadius:.045)*p.Stats["projectileSize"]);
            var start=ground+Vector3.up*(radius+.04f);float overhead=12+radius+.04f;
            // Probe upward from the struck floor, not down through a roof. The projectile still
            // sweeps normal collision, so intervening cover continues to block damage.
            if(root.scene.GetPhysicsScene().SphereCast(start,radius,Vector3.up,out var hit,12,WorldMask,QueryTriggerInteraction.Ignore))
                overhead=hit.distance+2*radius+.04f;
            return ground+Vector3.up*NativeBowRules.RainSpawnHeight(Vec(ground),radius,overhead);
        }
        private bool BowBusy(NativePilot p)=>p.Hp<=0||!p.Seat.connected||Incapacitated(p)||Time<p.BlockEnd||Time<p.RollEnd||Time<p.AttackEnd||KernelLocked(p);
        private void StepBow(NativePilot p)
        {
            if(p.Seat.spec!="bow")return;
            if(p.BowRainAt>-90&&p.Hp>0&&p.Seat.connected)
                while(p.BowRainNext<12&&Time>=p.BowRainAt+.6f+p.BowRainNext*.1f)
                {var at=p.BowRainTarget+Vec(NativeBowRules.RainOffset(p.BowRainNext++));Fire(p,bow:new NativeBowShot{Draw=.7f,Damage=9,Speed=90,Gravity=14,Impulse=120,Detail="rain",Origin=BowRainOrigin(p,at),Direction=Vector3.down});}
            if(BowBusy(p)||p.Input.Block||p.Input.Aim||p.Input.Sprint||p.Input.CancelAbilities||p.Input.AbilityQ||p.Input.AbilityE){p.BowDrawAt=-1;return;}
            if(p.BowDrawAt<0){if(p.Input.Shoot&&Time>=p.Ready){p.BowDrawAt=Time;p.BowDraw=0;}return;}
            if(p.Input.Shoot)return;
            float held=Time-p.BowDrawAt,rate=(float)p.Stats["attackRate"],draw=NativeBowRules.Fraction(held,rate);p.BowDraw=draw;p.BowReleaseAt=Time;p.BowDrawAt=-1;
            Fire(p,bow:new NativeBowShot{Draw=draw,Damage=NativeBowRules.Damage(draw),Speed=NativeBowRules.Speed(draw),Gravity=NativeBowRules.Gravity(draw),Spread=NativeBowRules.Spread(held,rate),Impulse=NativeBowRules.Impulse(draw),Detail=draw>=1?"bow-full":"bow"});p.Ready=Time+NativeBowRules.Nock;
        }
        private void StepBowAbilities(NativePilot p)
        {
            if(BowBusy(p)||p.Input.Block||p.Input.Roll||p.Input.CancelAbilities||Time<p.Ready)return;
            if(p.Input.AbilityQ&&Time>=p.QReady)
            {
                p.BowDrawAt=-1;p.QReady=Time+8*(float)p.Stats["abilityCooldown"];p.Pose.AbilityId="volley";p.Pose.AbilityAt=p.Pose.AbilityRelease=Time;
                for(int i=0;i<5;i++)Fire(p,bow:new NativeBowShot{Draw=.7f,Damage=22,Speed=90,Gravity=14,Impulse=246,Detail="volley",Yaw=(i-2)*3.5f});p.Ready=Time+NativeBowRules.Nock;
            }
            else if(p.Input.AbilityE&&Time>=p.EReady)
            {
                p.BowDrawAt=-1;p.EReady=Time+12*(float)p.Stats["abilityCooldown"];p.BowRainAt=Time;p.BowRainNext=0;p.BowRainTarget=GroundPoint(AimedAbilityPoint(p,40));
                p.Pose.AbilityId="rain-of-arrows";p.Pose.AbilityAt=Time;p.Pose.AbilityRelease=Time+.6f;p.Ready=Time+NativeBowRules.Nock;
                Emit(EventKind.AbilityCast,p,0,p.BowRainTarget,0,"rain",NativeBowRules.RainRadius);
            }
        }
    }
}
