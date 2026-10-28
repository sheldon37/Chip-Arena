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
            var bounds=Map.bounds;Map.bounds=new MapBounds{minX=950,maxX=1050,minZ=950,maxZ=1050,ceiling=200,killY=-6};
            var a=Pilots[0];var b=Pilots[1];var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Special projectile cover check";wall.transform.SetParent(root.transform);wall.layer=PropLayer;
            wall.transform.position=new Vector3(1000,100,1001);wall.transform.localScale=new Vector3(3,3,.2f);wall.SetActive(false);
            void Check(bool ok,string label){if(!ok)throw new Exception("Native special gate: "+label);}
            void Advance(float dt){while(dt>.000001f){float step=Math.Min(dt,.05f);Match.Tick(step);dt-=step;}}
            void Position(NativePilot p,float z){p.Body.position=new Vector3(1000,100,z);p.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
            void Setup(string spec)
            {
                wall.SetActive(false);meleeFissures.Clear();meleeCarries.Clear();Events.Clear();Match.Teams=false;MeleeShots.Clear();Bullets.Clear();AbilityShots.Clear();MeleeImpacts.Clear();
                foreach(var p in new[]{a,b})
                {
                    p.Seat.spec=p==a?spec:"sword";p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new System.Collections.Generic.Dictionary<string,int>());
                    p.Seat.bot=false;p.Seat.connected=p.Seat.alive=true;p.Hp=p.Stamina=p.Mana=100;p.Body.isKinematic=false;p.Collider.enabled=true;
                    p.MeleeKit=new NativeMeleeKit();p.MeleeBufferedUntil=-1;p.ArmourHitUntil=0;p.Swing=null;p.Special=null;p.Down=null;p.Roll=null;p.Knockback=p.MoveVelocity=Vector3.zero;
                    p.Ready=p.QReady=p.EReady=p.AttackEnd=p.BlockEnd=p.BlockReady=p.RollEnd=p.RollReady=p.StunEnd=p.StaggerImmune=p.ReloadEnd=p.CastAt=0;
                    p.SlowFactor=1;p.SlowEnd=0;p.Juggle=null;p.RiposteAt=p.BlockAt=-100;p.RiposteBy=-1;p.Dots.Clear();CancelAbilities(p);p.Grounded=true;
                    p.Input=new InputFrame{Yaw=p==a?0:180,AbilityTarget=new V3(1000,100,1020)};p.Yaw=p.Input.Yaw;Position(p,p==a?1000:1002);
                }
            }
            NativeMeleeShot Release(string spec)
            {
                Setup(spec);Position(b,1010);Check(TrySpecial(a),"throw setup");Advance(a.Special.Release-Time+.001f);StepSpecial(a);Check(MeleeShots.Count==1,"throw release");return MeleeShots[0];
            }
            try
            {
                // Exercise the normal input dispatcher used by StepPilot for every melee Q/E.
                // These commit stamina immediately and release Special, never a mana AbilityCast.
                foreach(string spec in new[]{"sword","daggers","shield","greatsword"})foreach(string slot in new[]{"q","e"})
                {
                    Setup(spec);Position(b,1020);a.Mana=0;a.Stamina=0;a.ECharges=1;
                    var ability=Content.Ability(spec,slot);Check(ability!=null&&ability.live,"mapped live melee "+spec+"/"+slot);
                    a.Input.AbilityQ=slot=="q";a.Input.AbilityE=slot=="e";StepAbilities(a);
                    Check(a.Special==null&&a.AbilityCast==null&&a.QReady==0&&a.EReady==0&&a.ECharges==1,"unfunded melee dispatch "+ability.id);
                    a.Stamina=100;StepAbilities(a);var action=a.Special;
                    Check(action!=null&&action.Fx==ability.id&&a.AbilityCast==null,"melee dispatch "+ability.id);
                    NearNative(a.Stamina,100-NativeAbilityRules.Cost(ability,a.Stats),"dispatch stamina "+ability.id);
                    NearNative((slot=="q"?a.QReady:a.EReady)-Time,NativeAbilityRules.Cooldown(ability,a.Stats),"dispatch cooldown "+ability.id);
                    Check(a.Mana==0&&a.ECharges==(slot=="e"?0:1),"dispatch resource pool "+ability.id);
                    a.Input.AbilityQ=a.Input.AbilityE=false;StepSpecial(a);Check(!action.Released,"melee early release "+ability.id);
                    Advance(action.Release-Time+.001f);StepAbilities(a);StepSpecial(a);
                    Check(action.Released&&a.AbilityCast==null&&a.Mana==0,"melee dispatch release "+ability.id);
                    if(action.Phase=="recovery")Check(a.MeleeKit.RecoverAt>=action.Release&&a.MeleeKit.RecoverUntil>=a.MeleeKit.RecoverAt,"replicated recovery clocks "+ability.id);
                    NearNative(a.Stamina,100-NativeAbilityRules.Cost(ability,a.Stats),"melee release charged twice "+ability.id);
                }
                Debug.Log("PASS native melee Q/E dispatcher: all eight abilities reject empty stamina, cast with zero mana, debit once at commitment and release through Special");
                foreach(string spec in new[]{"sword","daggers","shield","greatsword"})
                {
                    Setup(spec);a.Stamina=0;Check(!TrySpecial(a)&&a.Special==null&&a.EReady==0,"unfunded E spent cooldown");a.Stamina=100;
                    a.StunEnd=Time+1;Check(!TrySpecial(a),"E during stun");a.StunEnd=0;
                    if(spec=="greatsword"){a.Grounded=false;Check(!TrySpecial(a),"airborne Whirlwind");a.Grounded=true;}
                    Check(TrySpecial(a),"E start "+spec);NearNative(a.Stamina,100-NativeAbilityRules.Cost(Content.Ability(spec,"e"),a.Stats),"E resource");NearNative(a.EReady-Time,NativeAbilityRules.Cooldown(Content.Ability(spec,"e"),a.Stats),"E cooldown");
                    Check(!TrySpecial(a)&&!TryMelee(a)&&!TryBlock(a),"committed special interrupted");
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

                ValidateWhirlwind(Setup,Advance,Position,a,b,wall);
                ValidateKunaiAim(Setup,Advance,a,b,wall);

                var dagger=Release("daggers");dagger.Position=new Vector3(1000,100,1000);dagger.Velocity=Vector3.forward*120;wall.SetActive(true);Physics.SyncTransforms();StepMeleeShots();Check(MeleeShots.Count==0&&b.Hp==100,"dagger tunneled through cover");
                dagger=Release("daggers");Position(b,1002);dagger.Position=new Vector3(1000,100,1000);dagger.Velocity=Vector3.forward*120;Physics.SyncTransforms();StepMeleeShots();Check(b.Hp<100&&MeleeShots.Count==0,"swept dagger missed capsule");
                dagger=Release("daggers");wall.SetActive(true);dagger.Position=wall.transform.position;Physics.SyncTransforms();StepMeleeShots();Check(MeleeShots.Count==0,"starting overlap missed cover");

                dagger=Release("daggers");b.BlockEnd=Time+1;dagger.Position=b.Body.position;dagger.Velocity=Vector3.forward*80;b.Stats["reflectSpeed"]=3;b.Stats["reflectDamage"]=10;
                var contact=new MeleeContact{Collider=b.Collider,Point=b.Body.position,Normal=Vector3.back};
                float life=dagger.Life,travel=dagger.Travel;Check(!HitMeleeShot(dagger,contact)&&dagger.Owner==b&&dagger.Reflected&&dagger.Velocity.z<0,"dagger reflection");NearNative(dagger.Velocity.magnitude,120,"reflect speed cap");NearNative(dagger.Damage,100,"reflect damage cap");Check(dagger.Life==life&&dagger.Travel==travel,"reflection refilled shot");dagger.Reflections=3;Check(HitMeleeShot(dagger,contact),"fourth reflection not absorbed");

                var shield=Release("shield");Position(b,1002);shield.Position=new Vector3(1000,100,1000);shield.Velocity=Vector3.forward*120;StepMeleeShots();Check(shield.Returning&&b.Hp<100&&b.Down!=null,"shield hit/return");
                hp=b.Hp;float stamina=a.Stamina;shield.Position=b.Body.position;StepMeleeShots();Check(b.Hp==hp&&a.Stamina==stamina,"return damaged/refunded");
                shield.Position=a.Body.position+Vector3.up*.25f;StepMeleeShots();Check(MeleeShots.Count==0,"shield catch");Check(!a.MeleeKit.ShieldOut||!MeleeShots.Exists(s=>s.Original==a&&s.Shield),"catch left shield away");
                shield=Release("shield");Advance(.6f);StepSpecial(a);Check(MeleeShots.Count==1,"flying shield expired");Check(!TryBlock(a)&&MeleeShots.Count==1,"block allowed while shield away");
                a.Hp=0;StepMeleeShots();Check(MeleeShots.Count==0,"dead owner shield leaked");
                shield=Release("shield");b.BlockEnd=Time+1;contact=new MeleeContact{Collider=b.Collider,Point=b.Body.position,Normal=Vector3.back};HitMeleeShot(shield,contact);Check(shield.Returning&&b.Hp==100,"shield block");
                // Q/E additions exercise the real handlers in the same isolated physics scene.
                foreach(string spec in new[]{"sword","daggers","shield","greatsword"})
                {
                    Setup(spec);a.Stamina=0;Check(!TryMeleeAbility(a,"q")&&a.QReady==0,"unfunded Q");a.Stamina=100;
                    Check(TryMeleeAbility(a,"q")&&!TryMeleeAbility(a,"e"),"Q commitment "+spec);
                    NearNative(a.Stamina,100-NativeAbilityRules.Cost(Content.Ability(spec,"q"),a.Stats),"Q stamina");
                    Check(Events.Exists(e=>e.Kind==EventKind.AbilityCast&&e.Detail==Content.Ability(spec,"q").fx),"cast fx tag");
                    Check(TryRoll(a)&&a.Special==null,"windup roll cancellation");
                }
                Setup("sword");Position(b,1001);TryMeleeAbility(a,"q");Advance(a.Special.Release-Time+.001f);StepSpecial(a);
                Check(!TryRoll(a),"active dash roll cancellation");MeleeAbilityMotion(a);NearNative(b.Hp,74,"Flash Step budget");Check(a.QReady-Time<3&&a.Special.Phase=="recovery","Flash Step refund/stop");
                Setup("sword");TryMeleeAbility(a,"e");Advance(a.Special.Release-Time+.001f);StepSpecial(a);
                Check(MeleeRecastOpen(a)&&TryMeleeRecast(a),"Skyfall recast");Advance(.201f);StepSpecial(a);NearNative(b.Hp,44.4f,"Skyfall combined budget");Check(a.Special.Phase=="fall"&&b.Down.PickupUsed,"Skyfall slam");
                Advance(.06f);b.Grounded=true;StepMeleeKitWorld();Check(a.Special.Phase=="recovery"&&Events.Exists(e=>e.Kind==EventKind.Land&&e.Detail=="rising-dragon"),"Skyfall ground bounce/land");
                Setup("sword");TryMeleeAbility(a,"e");float castAt=Time;Advance(a.Special.Release-Time+.001f);StepSpecial(a);Advance(.901f);StepMeleeAbilityInput(a);NearNative(a.EReady,castAt+7,"unrecast cooldown");
                Setup("daggers");Position(b,1020);TryMeleeAbility(a,"q");Advance(a.Special.Release-Time+.001f);StepSpecial(a);NearNative(a.Body.position.z,1006,"Shadow Step displacement");Check(a.MeleeKit.AfterimageUntil>Time&&Events.Exists(e=>e.Kind==EventKind.Teleport&&e.Detail=="shadow-step"&&e.From.z==1000),"afterimage/teleport contract");
                Position(b,1008);float boosted=MeleeAbilityHit(a,b.Seat.id,18,Vector3.forward,0,"kunai-mark",b.Collider.ClosestPoint(a.Body.position));NearNative(boosted,25.2f,"next dagger hit empowerment");Check(a.MeleeKit.EmpowerUntil==0,"empower consumed once");
                Setup("daggers");Position(b,1020);wall.SetActive(true);Physics.SyncTransforms();TryMeleeAbility(a,"q");Advance(a.Special.Release-Time+.001f);StepSpecial(a);Check(a.Body.position.z<1001,"blink crossed cover");
                dagger=Release("daggers");Position(b,1004);HitMeleeShot(dagger,new MeleeContact{Collider=b.Collider,Point=b.Collider.ClosestPoint(a.Body.position),Normal=Vector3.back});Check(MeleeRecastOpen(a)&&a.MeleeKit.MarkTarget==b.Seat.id,"kunai mark");Advance(.4f);StepSpecial(a);Check(TryMeleeRecast(a),"kunai recast");Advance(.201f);StepSpecial(a);NearNative(b.Hp,54,"kunai combined damage");Check(a.MeleeKit.MarkTarget==0&&a.Body.position.z>b.Body.position.z,"mark consumed / teleport behind");Check(a.Special.Phase=="recovery"&&a.MeleeKit.RecoverAt>=a.Special.Release&&a.MeleeKit.RecoverUntil>a.MeleeKit.RecoverAt,"kunai recast recovery clocks");
                Setup("shield");Position(b,1020);TryMeleeAbility(a,"q");Advance(a.Special.Release-Time+.001f);StepSpecial(a);
                Check(!AbsorbCharge(a,Vector3.forward,a.Body.position,1),"rear projectile absorbed");
                for(int shot=1;shot<=3;shot++)Check(AbsorbCharge(a,Vector3.back,a.Body.position,shot),"frontal absorption "+shot);
                Check(a.MeleeKit.Absorbed==3&&a.Special.Phase=="recovery"&&!AbsorbCharge(a,Vector3.back,a.Body.position,4),"absorb cap/end");
                Setup("shield");Position(b,1001);wall.transform.position=new Vector3(1000,100,1002.5f);wall.SetActive(true);Physics.SyncTransforms();TryMeleeAbility(a,"q");Advance(a.Special.Release-Time+.001f);StepSpecial(a);MeleeAbilityMotion(a);for(int tick=0;tick<24;tick++){Advance(1f/60);StepMeleeKitWorld();}NearNative(b.Hp,70,"charge plus wall budget");Check(b.StunEnd>Time,"wall stun");Check(Events.Exists(e=>e.Kind==EventKind.AbilityHit&&e.Detail=="bulwark-charge"&&e.WallSlam),"explicit wall slam flag");wall.transform.position=new Vector3(1000,100,1001);
                Setup("greatsword");Position(b,1004);TryMeleeAbility(a,"q");Advance(a.Special.Release-Time+.001f);StepSpecial(a);Advance(.16f);StepMeleeKitWorld();NearNative(b.Hp,70,"fissure front hit");Advance(.1f);StepMeleeKitWorld();NearNative(b.Hp,70,"fissure hit once");Check(b.SlowFactor==.7f&&a.MeleeKit.FissureSpeed==25,"fissure trail contract");

                Setup("sword");Position(b,1020);Check(TryMelee(a),"buffer setup");var first=a.Swing;float afterFirst=a.Stamina;Advance(first.HitUntil-Time-.001f);a.Input.Attack=true;StepMeleeInput(a);a.Input.Attack=false;Check(a.Swing==first,"buffer cut active");Advance(NativeMeleeKitRules.ChainAt(first)-Time+.001f);StepMeleeInput(a);Check(a.Swing!=first&&a.Swing!=null&&Time<first.Until,"released button lost queued chain");NearNative(a.Stamina,afterFirst-NativeMeleeFeel.Cost(a.Weapon,a.Stats),"per-chain stamina");
                Setup("sword");Position(b,1020);TryMelee(a);first=a.Swing;a.Stamina=0;a.Input.Attack=true;Advance(NativeMeleeKitRules.ChainAt(first)-Time+.001f);StepMeleeInput(a);Check(a.Swing==first&&a.Stamina==0,"unfunded chain");
                Setup("sword");TryBlock(b);Damage(b,a,10,Vector3.forward,0,false,melee:true,hitPoint:b.Collider.ClosestPoint(a.Body.position));Check(Events.Exists(e=>e.Kind==EventKind.Block&&e.Detail=="guard")&&Events.Exists(e=>e.Kind==EventKind.Block&&e.Detail=="clash"&&e.Target==a.Seat.id),"block semantic tags/attacker");
                Debug.Log("PASS native melee kit: Q/E funding and commitment, recasts, blink cover, Whirlwind, charge absorption/carry/wall cap, fissure once-only hits/slow, input buffering and per-swing stamina");
                Debug.Log("PASS native E specials: costs/gates, launcher/pickup, juggle cap, protected recovery, Whirlwind ticks/recovery, swept throws/overlaps/cover, reflection caps, harmless shield return/catch and owner cleanup");
            }
            finally{Map.bounds=bounds;CapturedAnimation.Release(wall);MeleeShots.Clear();MeleeImpacts.Clear();meleeFissures.Clear();meleeCarries.Clear();}
        }
    }
}
#endif
