using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed class NativeLeetField
    {public int Id;public string Kind;public NativePilot Owner,Target;public NativeMinion Slender;public Vector3 Position,From;public float At,Until,Sanity,HopAt,LostAt,Yaw;public int Hops;public bool Seen;}
    public sealed partial class NativePilot
    {
        public NativePilot Controlling,ControlledBy,ScareBy;public float ControlUntil,FrozenUntil,ScareAt,ScareUntil,LaunchUntil;
        public readonly Queue<(float time,Vector3 position)> Rewind=new Queue<(float,Vector3)>();public Vector3? FrozenPosition;
    }
    public sealed partial class NativeArena
    {
        public readonly List<NativeLeetField> LeetFields=new List<NativeLeetField>();private readonly Dictionary<NativePilot,InputFrame> originalInputs=new Dictionary<NativePilot,InputFrame>();
        public NativePilot ViewPilot=>Local?.Controlling??Local;
        private bool Leet(NativePilot p,string chip=null)=>Dev(p,"leet")&&(chip==null||Has(p,"l44t-"+chip));
        private bool Frozen(NativePilot p)=>p!=null&&(Time<p.FrozenUntil||Time<p.ScareUntil||ExpeditionChoosing(p));
        private void ReleaseControl(NativePilot p)
        {var controller=p.Controlling!=null?p:p.ControlledBy;var target=controller?.Controlling;if(controller!=null){controller.Controlling=null;controller.ControlUntil=0;}if(target!=null){target.ControlledBy=null;target.ControlUntil=0;}}
        private void RouteLeetInputs()
        {
            originalInputs.Clear();foreach(var p in Pilots)
            {if(p.Controlling!=null&&(p.Hp<=0||!p.Seat.connected||p.Controlling.Hp<=0||!p.Controlling.Seat.connected||Time>=p.ControlUntil||!Leet(p,"brain")))ReleaseControl(p);}
            foreach(var p in Pilots)if(p.Controlling!=null){var q=p.Controlling;originalInputs[p]=p.Input;originalInputs[q]=q.Input;q.Input=p.Input;p.Input=new InputFrame{Yaw=p.Yaw,Pitch=p.Pitch};}
            foreach(var p in Pilots)if(Frozen(p)){if(ExpeditionChoosing(p)){p.Yaw=p.Input.Yaw;p.Pitch=p.Input.Pitch;p.RollEnd=0;}if(!p.FrozenPosition.HasValue)p.FrozenPosition=p.Body.position;if(!originalInputs.ContainsKey(p))originalInputs[p]=p.Input;p.Input=new InputFrame{Yaw=p.Yaw,Pitch=p.Pitch};CancelFighter(p);CancelMelee(p);CancelSpecial(p);CancelAbilities(p);p.BlockEnd=0;p.MoveVelocity=p.Knockback=Vector3.zero;p.Body.linearVelocity=Vector3.zero;}
        }
        private void RestoreLeetInputs()
        {
            foreach(var p in Pilots){if(Frozen(p)&&p.Hp>0&&p.FrozenPosition.HasValue){p.Body.position=p.FrozenPosition.Value;p.Body.linearVelocity=Vector3.zero;p.Body.useGravity=false;}else p.FrozenPosition=null;}
            foreach(var pair in originalInputs)pair.Key.Input=pair.Value;originalInputs.Clear();
        }
        private NativePilot LeetTarget(NativePilot owner,Vector3 point)=>Pilots.Where(p=>p.Hp>0&&p.Seat.connected&&Enemy(owner,p)&&Vector3.Distance(p.Body.position,point)<=28&&MeleeVisible(point,p.Body.position,owner,p.Collider)).OrderBy(p=>(p.Body.position-point).sqrMagnitude).FirstOrDefault();
        private void Possess(NativePilot owner,NativePilot target)
        {if(!Leet(owner,"brain")||!Enemy(owner,target)||target.Hp<=0||!target.Seat.connected||owner.ControlledBy!=null||target.Controlling!=null||target.ControlledBy!=null)return;ReleaseControl(owner);owner.Controlling=target;target.ControlledBy=owner;target.ControlUntil=owner.ControlUntil=Time+5;CancelAbilities(owner);CancelMelee(owner);CancelAbilities(target);CancelMelee(target);Fx(target.Body.position,1,2,owner.Body.position,owner,"leet-control");}
        private void LeetOrb(NativePilot p,Vector3 position,Vector3 dir,string kind,Vector3? target=null,float release=0)
        {if(ProjectileCount>=100)return;Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Spec=p.Seat.spec,Born=Time,Position=position,Velocity=dir*(kind=="brain"?23:32),Damage=kind=="brain"?0:24,Impulse=180,Life=7,Radius=kind=="brain"?.27f:.16f,Direct=false,ChipImpact=kind=="copy",EffectsUsed=true,Travel=100,LeetKind=kind,LockTarget=target,ReleaseAt=release,LeetLaunched=kind=="brain"});}
        private bool LeetBlock(NativePilot p)
        {if(!Leet(p,"brain"))return false;CancelAbilities(p);CancelMelee(p);p.BlockAt=Time;p.BlockReady=Time+6;p.BlockEnd=0;Vector3 dir=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));LeetOrb(p,p.Body.position+Vector3.up*.2f+dir*.8f,dir,"brain");return true;}
        private void StepLeetPilot(NativePilot p)
        {
            if(!Leet(p)||!p.Input.AbilityE||AbilityBusy(p)||Time<p.Ready||Time<p.EReady||Time<p.BlockEnd||Frozen(p))return;float cost=30*(float)p.Stats["resourceCost"];if(p.Mana<cost)return;p.Mana-=cost;p.EReady=Time+12;p.Ready=Time+.4f;p.LastAttack=p.AttackAt=Time;
            if(Leet(p,"time")){foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q)&&!SirenArmor(q))q.FrozenUntil=Time+4;Emit(EventKind.Leet,p,0,p.Body.position,0,"leet-freeze",28);}
            if(Leet(p,"scare")){foreach(var f in LeetFields.Where(f=>f.Owner==p&&f.Kind=="tree").ToArray())RemoveLeetField(f);Vector3 pos=p.Body.position+Vec(V3.Forward(p.Input.Yaw))*2;pos.y=.05f;LeetFields.Add(new NativeLeetField{Id=nextEvent++,Owner=p,Kind="tree",Position=pos,At=Time,Until=Time+22});}
            if(!Leet(p,"time")&&!Leet(p,"scare"))ChipBlast(p,p.Body.position,6,35,1200,effect:"leet-block");
        }
        private void LeetSpawned(NativePilot p,List<NativeBullet> shots)
        {if(!Leet(p))return;foreach(var b in shots){b.LeetKind="bolt";b.Homing=b.Gravity=0;b.Boomerang=b.Zigzag=false;b.Life=7;}}
        private bool LeetShotStep(NativeBullet b,NativePilot owner,float dt)
        {
            if(Frozen(owner))return false;if(b.LeetKind==null||b.LeetKind=="brain"||b.LeetLaunched)return true;if(b.Reflections>0){b.LeetLaunched=true;return true;}
            if(!b.LockTarget.HasValue){var enemy=LeetTarget(owner,b.Position);if(enemy==null)return true;b.LockTarget=enemy.Body.position;b.ReleaseAt=Time+.42f;b.FixedSpeed=Math.Max(25,b.Velocity.magnitude);b.Velocity=Vector3.zero;Fx(b.Position,.25f,2,b.LockTarget.Value,owner,"leet-lock");}
            if(Time<b.ReleaseAt){b.Life-=dt;return false;}b.Velocity=(b.LockTarget.Value-b.Position).normalized*(b.FixedSpeed>0?b.FixedSpeed:32);b.LeetLaunched=true;return true;
        }
        private void LeetImpact(NativePilot p,NativeBullet b,Vector3 at)
        {
            if(Leet(p))Emit(EventKind.Leet,p,0,at,b.Damage,b.LeetKind);
            if(!Leet(p)||b.Reflections>0||!(b.Direct||b.ChipImpact)||!ProcOnce("leet:"+b.Id))return;
            if(Leet(p,"nuke"))LeetFields.Add(new NativeLeetField{Id=nextEvent++,Owner=p,Kind="nuke",Position=at,At=Time,Until=Time+1.4f});
            if(b.Direct&&Leet(p,"copy"))for(int i=0;i<3;i++)LeetOrb(p,at+new Vector3((i-1)*1.1f,2.5f,.4f),Vector3.zero,"copy",at,Time+.5f+i*.16f);
            if(Leet(p,"uprising")){var q=LeetTarget(p,at);if(q!=null)foreach(var prop in Props.Where(x=>x.Body!=null&&!x.Body.isKinematic&&!x.Indestructible&&Vector3.Distance(x.Body.position,at)<10).Take(8))prop.Body.AddForce((q.Body.position+Vector3.up*.3f-prop.Body.position).normalized*prop.Body.mass*45,ForceMode.Impulse);}
        }
        private bool LeetSave(NativePilot p,float damage)
        {if(!Leet(p,"undo")||damage<p.Hp||!ProcOnce("undo:"+p.Seat.id))return false;if(p.Rewind.Count>0)p.Body.position=p.Rewind.Peek().position;p.Hp=(float)p.Stats["hp"];p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;p.Dots.Clear();return true;}
        private void RemoveLeetField(NativeLeetField f){if(f.Slender!=null)RemoveMinion(f.Slender,false);LeetFields.Remove(f);}
        private void StepLeetWorld()
        {
            foreach(var p in Pilots)
            {
                if(p.ScareBy!=null&&Time>=p.ScareUntil){var source=p.ScareBy;p.ScareBy=null;p.ScareUntil=0;Damage(p,source,p.Hp+1,Vector3.zero,0,false,forced:true,primary:false);}
                if(Leet(p,"undo")&&p.Hp>0&&((int)Math.Round(Time*60))%6==0){Vector3 at=p.Body.position;if(at.y>0&&at.y<8&&Math.Abs(at.x)<30&&Math.Abs(at.z)<26){p.Rewind.Enqueue((Time,at));while(p.Rewind.Count>0&&p.Rewind.Peek().time<Time-3)p.Rewind.Dequeue();}}
            }
            foreach(var f in LeetFields.ToArray())
            {
                if(!f.Owner.Seat.connected||f.Kind=="tree"&&f.Owner.Hp<=0){RemoveLeetField(f);continue;}
                if(Time>=f.Until){RemoveLeetField(f);if(f.Kind=="nuke"){foreach(var q in Pilots)if(CanHit(f.Owner,q)&&Vector3.Distance(q.Body.position,f.Position)<11)q.LaunchUntil=Time+1;ChipBlast(f.Owner,f.Position,11,160,42000,effect:"leet-nuke");}continue;}
                if(f.Kind!="tree"||Time-f.At<.9f)continue;
                if(f.Slender==null)
                {
                    Vector3 feet=f.Position+new Vector3(.9f,0,.2f);var obj=new GameObject("Slenderman");obj.layer=ActorLayer;obj.transform.SetParent(root.transform);obj.transform.position=feet+Vector3.up*1.23f;var c=obj.AddComponent<CapsuleCollider>();c.radius=.28f;c.height=2.46f;var body=obj.AddComponent<Rigidbody>();body.isKinematic=true;
                    f.Slender=new NativeMinion{Id=nextMinion++,Owner=f.Owner,Kind="slender",Hp=160,MaxHp=160,Body=body,Collider=c,Until=f.Until,NextAttack=float.PositiveInfinity};obj.AddComponent<NativeMinionBody>().Minion=f.Slender;Minions.Add(f.Slender);f.HopAt=Time+2.6f;f.LostAt=Time;
                }
                var m=f.Slender;if(m.Hp<=0){RemoveLeetField(f);continue;}var target=f.Target;
                if(target==null||target.Hp<=0||!target.Seat.connected||target.ScareBy!=null||!Enemy(f.Owner,target)){target=Pilots.Where(q=>q.Hp>0&&q.Seat.connected&&q.ScareBy==null&&Enemy(f.Owner,q)).OrderBy(q=>(q.Body.position-m.Body.position).sqrMagnitude).FirstOrDefault();if(target!=f.Target){f.Target=target;f.Sanity=0;}}
                if(target==null){f.Seen=false;continue;}Vector3 center=m.Body.position,feetAt=center-Vector3.up*1.23f,tp=target.Body.position,eye=tp+Vector3.up*.52f,look=Vec(GunRules.Direction(target.Input.Yaw,target.Input.Pitch));bool seen=false;
                foreach(float y in new[]{2.2f,1.35f}){Vector3 point=feetAt+Vector3.up*y,d=point-eye;if(d.magnitude<=46&&Vector3.Dot(d.normalized,look)>=Mathf.Cos(36*Mathf.Deg2Rad)&&MeleeVisible(eye,point,target,m.Collider)){seen=true;break;}}
                float gap=new Vector2(tp.x-center.x,tp.z-center.z).magnitude;m.Yaw=f.Yaw=Mathf.Atan2(tp.x-center.x,tp.z-center.z)*Mathf.Rad2Deg;
                if(seen)f.Sanity=Math.Min(1,f.Sanity+(1f/60)*(.09f+.5f*Mathf.Clamp01(1-gap/14)));else{if(f.Seen)f.LostAt=Time;f.Sanity=Math.Max(0,f.Sanity-.3f/60);}f.Seen=seen;
                if(gap<=1.6f||f.Sanity>=1){target.ScareBy=f.Owner;target.ScareAt=Time;target.ScareUntil=Time+2.2f;CancelAbilities(target);CancelMelee(target);target.BlockEnd=0;RemoveLeetField(f);continue;}
                if(!seen&&Time>=f.HopAt&&Time-f.LostAt>=.35f)
                {
                    float aggression=Mathf.Clamp01((Time-f.At)/Math.Max(1,f.Until-f.At)),noise=Mathf.Sin((f.Hops+1)*12.9898f+f.At*78.233f)*43758.5453f,r=noise-Mathf.Floor(noise),angle=target.Input.Yaw+180+(r-.5f)*2*.85f*Mathf.Rad2Deg,want=Math.Max(1.35f,gap*Mathf.Lerp(.72f,.5f,aggression));Vector3 dir=Vec(V3.Forward(angle));float distance=want;
                    if(root.scene.GetPhysicsScene().Raycast(tp+Vector3.up*.2f,dir,out var hit,want,WorldMask))distance=Math.Max(Math.Min(want,1.35f),hit.distance-.45f);
                    Vector3 spot=tp+dir*distance;spot=ClampMap(spot);spot.y=tp.y-.85f;f.From=feetAt;f.Hops++;f.HopAt=Time+Mathf.Lerp(3.2f,1.3f,aggression);m.Body.position=spot+Vector3.up*1.23f;Fx(spot,1,2,feetAt,f.Owner,"slender-hop");
                }
            }
        }
    }
}
