#if UNITY_EDITOR
using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Actual handlers and PhysicsScene queries. Compiling this gate does not execute it.
        public void ValidateSpecials()
        {
            var a=Pilots[0];var b=Pilots[1];var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Special projectile cover check";wall.transform.SetParent(root.transform);wall.layer=PropLayer;
            wall.transform.position=new Vector3(1000,100,1001);wall.transform.localScale=new Vector3(3,3,.2f);wall.SetActive(false);
            void Check(bool ok,string label){if(!ok)throw new Exception("Native special gate: "+label);}
            void Advance(float dt){while(dt>.000001f){float step=Math.Min(dt,.05f);Match.Tick(step);dt-=step;}}
            void Position(NativePilot p,float z){p.Body.position=new Vector3(1000,100,z);p.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
            void Setup(string spec)
            {
                wall.SetActive(false);Match.Teams=false;MeleeShots.Clear();Bullets.Clear();AbilityShots.Clear();MeleeImpacts.Clear();
                foreach(var p in new[]{a,b})
                {
                    p.Seat.spec=p==a?spec:"sword";p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new System.Collections.Generic.Dictionary<string,int>());
                    p.Seat.bot=false;p.Seat.connected=p.Seat.alive=true;p.Hp=p.Stamina=p.Mana=100;p.Body.isKinematic=false;p.Collider.enabled=true;
                    p.Swing=null;p.Special=null;p.Down=null;p.Roll=null;p.Knockback=p.MoveVelocity=Vector3.zero;
                    p.Ready=p.EReady=p.AttackEnd=p.BlockEnd=p.BlockReady=p.RollEnd=p.RollReady=p.StunEnd=p.StaggerImmune=p.ReloadEnd=p.CastAt=0;
                    p.RiposteAt=p.BlockAt=-100;p.RiposteBy=-1;p.Dots.Clear();CancelAbilities(p);p.Grounded=true;
                    p.Input=new InputFrame{Yaw=p==a?0:180,AbilityTarget=new V3(1000,100,1020)};p.Yaw=p.Input.Yaw;Position(p,p==a?1000:1002);
                }
            }
            NativeMeleeShot Release(string spec)
            {
                Setup(spec);Position(b,1010);Check(TrySpecial(a),"throw setup");Advance(a.Special.Release-Time+.001f);StepSpecial(a);Check(MeleeShots.Count==1,"throw release");return MeleeShots[0];
            }
            try
            {
                foreach(string spec in new[]{"sword","daggers","shield","greatsword"})
                {
                    Setup(spec);a.Stamina=0;Check(!TrySpecial(a)&&a.Special==null&&a.EReady==0,"unfunded E spent cooldown");a.Stamina=100;
                    a.StunEnd=Time+1;Check(!TrySpecial(a),"E during stun");a.StunEnd=0;
                    if(spec=="greatsword"){a.Grounded=false;Check(!TrySpecial(a),"airborne leap");a.Grounded=true;}
                    Check(TrySpecial(a),"E start "+spec);NearNative(a.Stamina,100-(float)a.Weapon.secondaryCost,"E resource");NearNative(a.EReady-Time,(float)a.Weapon.secondaryCooldown,"E cooldown");
                    Check(!TrySpecial(a)&&!TryMelee(a)&&!TryRoll(a)&&!TryBlock(a),"committed special interrupted");
                    StepSpecial(a);Check(!a.Special.Released&&MeleeShots.Count==0&&b.Hp==100,"special released early");
                }
                Setup("sword");TrySpecial(a);Advance(a.Special.Release-Time+.001f);StepSpecial(a);
                NearNative(b.Hp,100-30*1.12f,"launcher damage");Check(b.Down!=null&&!b.Grounded,"launcher knockdown");NearNative(b.Knockback.y,7.4f,"launcher lift");
                PrimaryMeleeImpact(a,b);PrimaryMeleeImpact(a,b);b.Knockback.y=0;PrimaryMeleeImpact(a,b);Check(b.Down.JuggleHits==2&&b.Knockback.y==0,"air follow-up cap");
                Advance(.19f);StepRecovery(b);Check(b.Down.Landed<0,"timer-only recovery landing");b.Grounded=true;StepRecovery(b);Check(NativeSpecialRules.Pickup(b.Down,Time),"missing pickup window");
                PrimaryMeleeImpact(a,b);Check(b.Down.PickupUsed&&b.Down.Landed<0,"pickup did not consume window");NearNative(b.Knockback.y,4.8f,"pickup lift");
                Advance(.19f);b.Grounded=true;StepRecovery(b);Check(NativeSpecialRules.Protected(b.Down,Time),"second pickup window granted");float hp=b.Hp;
                Damage(b,a,10,Vector3.forward,100,false,melee:true);Check(b.Hp==hp,"ground protection bypass");
                Advance(.401f);b.Input.X=1;StepRecovery(b);StepPilot(b);NearNative(b.Body.linearVelocity.x,6,"recovery travel");b.Input.X=-1;StepRecovery(b);StepPilot(b);NearNative(b.Body.linearVelocity.x,6,"recovery heading changed");
                Advance(.61f);StepRecovery(b);Check(b.Down==null&&!Incapacitated(b),"recovery failed to end");

                Setup("greatsword");Position(b,1010);TrySpecial(a);Advance(a.Special.Release-Time+.001f);StepSpecial(a);NearNative(a.Knockback.y,10,"leap kick");
                Position(b,1002);Advance(.05f);StepSpecial(a);float direct=b.Hp;Check(direct<100&&b.Down!=null,"air spin hit");
                Advance(.17f);a.Grounded=true;StepSpecial(a);Check(a.Special.Landed>=0&&MeleeImpacts.Count==1,"landing slam");Check(b.Hp==direct,"spin and slam double-hit");StepSpecial(a);Check(MeleeImpacts.Count==1,"duplicate landing effect");

                var dagger=Release("daggers");dagger.Position=new Vector3(1000,100,1000);dagger.Velocity=Vector3.forward*120;wall.SetActive(true);Physics.SyncTransforms();StepMeleeShots();Check(MeleeShots.Count==0&&b.Hp==100,"dagger tunneled through cover");
                dagger=Release("daggers");Position(b,1002);dagger.Position=new Vector3(1000,100,1000);dagger.Velocity=Vector3.forward*120;Physics.SyncTransforms();StepMeleeShots();Check(b.Hp<100&&MeleeShots.Count==0,"swept dagger missed capsule");
                dagger=Release("daggers");wall.SetActive(true);dagger.Position=wall.transform.position;Physics.SyncTransforms();StepMeleeShots();Check(MeleeShots.Count==0,"starting overlap missed cover");

                dagger=Release("daggers");b.BlockEnd=Time+1;dagger.Position=b.Body.position;dagger.Velocity=Vector3.forward*80;b.Stats["reflectSpeed"]=3;b.Stats["reflectDamage"]=10;
                var contact=new MeleeContact{Collider=b.Collider,Point=b.Body.position,Normal=Vector3.back};
                float life=dagger.Life,travel=dagger.Travel;Check(!HitMeleeShot(dagger,contact)&&dagger.Owner==b&&dagger.Reflected&&dagger.Velocity.z<0,"dagger reflection");NearNative(dagger.Velocity.magnitude,120,"reflect speed cap");NearNative(dagger.Damage,100,"reflect damage cap");Check(dagger.Life==life&&dagger.Travel==travel,"reflection refilled shot");dagger.Reflections=3;Check(HitMeleeShot(dagger,contact),"fourth reflection not absorbed");

                var shield=Release("shield");Position(b,1002);shield.Position=new Vector3(1000,100,1000);shield.Velocity=Vector3.forward*120;StepMeleeShots();Check(shield.Returning&&b.Hp<100&&b.Down!=null,"shield hit/return");
                hp=b.Hp;float stamina=a.Stamina;shield.Position=b.Body.position;StepMeleeShots();Check(b.Hp==hp&&a.Stamina==stamina,"return damaged/refunded");
                shield.Position=a.Body.position+Vector3.up*.25f;StepMeleeShots();Check(MeleeShots.Count==0,"shield catch");NearNative(a.Special.Until-Time,.15f,"catch recovery");
                shield=Release("shield");Advance(.6f);StepSpecial(a);Check(a.Special!=null&&a.Special.Until>Time,"flying shield action expired");Check(TryBlock(a)&&a.Special==null&&MeleeShots.Count==1,"block during return lost shield");
                a.Hp=0;StepMeleeShots();Check(MeleeShots.Count==0,"dead owner shield leaked");
                shield=Release("shield");b.BlockEnd=Time+1;contact=new MeleeContact{Collider=b.Collider,Point=b.Body.position,Normal=Vector3.back};HitMeleeShot(shield,contact);Check(shield.Returning&&b.Hp==100,"shield block");
                Debug.Log("PASS native E specials: costs/gates, launcher/pickup, juggle cap, protected recovery, real-landing/shared-ledger slam, swept throws/overlaps/cover, reflection caps, harmless shield return/catch and owner cleanup");
            }
            finally{CapturedAnimation.Release(wall);MeleeShots.Clear();MeleeImpacts.Clear();}
        }
    }
}
#endif
