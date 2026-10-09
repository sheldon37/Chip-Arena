#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Runs against actual isolated Unity colliders during the Windows builder's import gate.
        public void ValidateRangedAndMagic()
        {
            var a=Pilots[0];var b=Pilots[1];var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Projectile gate cover";wall.transform.SetParent(root.transform);wall.layer=PropLayer;wall.transform.position=new Vector3(1000,10,1001);wall.transform.localScale=new Vector3(3,3,.1f);
            void Check(bool ok,string name){if(!ok)throw new Exception("Native combat gate: "+name);}
            void Advance(float seconds){while(seconds>1e-6){float dt=Math.Min(.05f,seconds);Match.Tick(dt);seconds-=dt;}}
            void Setup(string spec)
            {
                wall.SetActive(false);Bullets.Clear();AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();MeleeShots.Clear();abilityEchoes.Clear();ChipFields.Clear();Match.Teams=false;
                foreach(var p in new[]{a,b}){CancelSiren(p);CancelFighter(p);CancelSpecial(p);CancelMelee(p);CancelAbilities(p);p.Seat.chips.Clear();p.Seat.spec=p==a?spec:"sword";p.Seat.bot=false;p.Seat.alive=p.Seat.connected=true;p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);p.Hp=p.Mana=p.Stamina=100;p.Body.isKinematic=false;p.Body.position=new Vector3(1000,10,p==a?1000:1002);p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;p.Collider.enabled=true;p.Down=null;p.Juggle=null;p.Roll=null;p.Death=null;p.Wounds.Clear();p.Dots.Clear();p.Ready=p.QReady=p.EReady=p.ReloadEnd=p.BlockEnd=p.RollEnd=p.StunEnd=p.FrozenUntil=p.ScareUntil=p.ShieldUntil=p.CastAt=p.AttackEnd=0;p.ECharges=1;p.Ammo=Magazine(p);p.Yaw=p==a?0:180;p.Pitch=0;p.Input=new InputFrame{Yaw=p.Yaw,AimTarget=Vec(b.Body.position)};p.RegenAt=Time+20;}
                a.Input=new InputFrame{Yaw=0,AimTarget=Vec(b.Body.position),AbilityTarget=Vec(b.Body.position)};Physics.SyncTransforms();
            }
            NativeBullet Shot(string spec){Setup(spec);a.Input.Shoot=true;StepPilot(a);if(a.Weapon.@class=="magic"){Check(Bullets.Count==0&&a.CastAt>Time,"magic released before windup "+spec);a.Input.Shoot=false;Advance(a.CastAt-Time+.001f);StepPilot(a);}Check(Bullets.Count==1,"primary release "+spec);return Bullets[0];}
            void CheckAbilityRelease(ContentAbility ability,float mana)
            {
                a.Mana=mana;float cost=NativeAbilityRules.Cost(ability,a.Stats);int charges=a.ECharges;
                PressAbility(a,ability.slot);Check(a.AbilityCast?.Definition==ability,"ability start "+ability.id);
                float release=a.AbilityCast.Release;
                Check(a.Mana==mana&&a.QReady==0&&a.EReady==0&&a.ECharges==charges,"ability spent before release "+ability.id);
                Advance(release-Time+.001f);StepAbilities(a);
                Check(a.AbilityCast==null,"ability did not release "+ability.id);
                Check(Math.Abs(a.Mana-(mana-cost))<.0003f,"mana after "+ability.id+": expected "+(mana-cost)+", got "+a.Mana);
                float ready=ability.slot=="q"?a.QReady:a.EReady;
                Check(Math.Abs(ready-(Time+NativeAbilityRules.Cooldown(ability,a.Stats)))<.0003f,"cooldown after "+ability.id);
                Check(a.ECharges==charges-(ability.slot=="e"?1:0),"charges after "+ability.id);
            }
            try
            {
                foreach(string spec in new[]{"pistol","rifle","machinegun","fire","water","lightning","darkness"})
                {
                    var shot=Shot(spec);Check(a.Weapon.@class=="magic"?a.Mana<100:a.Ammo==Magazine(a)-1,"resource debit "+spec);shot.Position=new Vector3(1000,10,1000.8f);shot.Velocity=Vector3.forward*120;shot.Gravity=0;StepBullets();Check(b.Hp<100,"swept primary missed "+spec);float hp=b.Hp;StepBullets();Check(b.Hp==hp,"duplicate impact "+spec);
                    shot=Shot(spec);shot.Position=new Vector3(1000,10,1000.8f);shot.Velocity=Vector3.forward*120;shot.Gravity=0;wall.SetActive(true);Physics.SyncTransforms();StepBullets();Check(b.Hp==100,"primary passed through cover "+spec);
                }
                Setup("rifle");a.Ammo=0;a.Input.Shoot=true;StepPilot(a);Check(Bullets.Count==0&&a.ReloadEnd>Time,"empty rifle did not reload");a.Input.Shoot=false;Advance(a.ReloadEnd-Time+.001f);StepPilot(a);Check(a.Ammo==Magazine(a)&&a.ReloadEnd==0,"reload completion");
                var reflected=Shot("pistol");b.BlockEnd=Time+1;b.Stats["reflectSpeed"]=10;b.Stats["reflectDamage"]=10;reflected.Position=b.Body.position;reflected.Velocity=Vector3.forward*240;float life=reflected.Life;StepBullets();Check(Bullets.Contains(reflected)&&reflected.Owner==b.Seat.id&&reflected.Velocity.z<0&&reflected.Damage<=100&&reflected.Velocity.magnitude<=120&&reflected.Life<life,"block reflection/limits");
                foreach(var ability in Content.abilities.Where(d=>d.live&&d.kind!="ads"))
                {
                    var weapon=Content.weapons.FirstOrDefault(w=>Content.Ability(w.id,ability.slot)?.id==ability.id);if(weapon==null)throw new Exception("Unmapped live ability: "+ability.id);
                    Setup(weapon.id);CheckAbilityRelease(ability,(float)a.Stats["maxMana"]);
                    Setup(weapon.id);
                    // Gun E abilities cost no mana: they must still cast from an empty pool.
                    if(NativeAbilityRules.Cost(ability,a.Stats)==0)CheckAbilityRelease(ability,0);
                    else
                    {
                        a.Mana=0;PressAbility(a,ability.slot);
                        Check(a.AbilityCast==null&&a.Mana==0&&a.QReady==0&&a.EReady==0&&a.ECharges==1,"unfunded ability spent state "+ability.id);
                    }
                }
                Setup("pistol");a.Stats["extraProjectiles"]=3;a.Stats["split"]=2;Fire(a);Check(Bullets.Count==4,"multishot volley");var parent=Bullets[0];SplitBullet(a,parent,b.Body.position,b.Collider.GetInstanceID());Check(Bullets.Count==8&&Bullets.Skip(4).All(x=>!x.Direct&&x.EffectsUsed&&x.Life<=1.2f&&x.Hit.Contains(b.Collider.GetInstanceID())),"split recursion/ledger");
                // A10 removes generic hit stun. Death choreography must remain correct both
                // with no stun and with a pre-existing stun, without the projectile changing it.
                foreach(bool grounded in new[]{true,false})foreach(bool stunned in new[]{false,true})
                {
                    Setup("pistol");b.Hp=1;b.Grounded=grounded;float stunEnd=stunned?Time+1:0;b.StunEnd=stunEnd;
                    string state="grounded="+grounded+", pre-existing stun="+stunned;
                    Damage(b,a,100,Vector3.forward,0,true,primary:false,impact:NativeBodyDamage.Projectile("rifle",140,.045f),hitPoint:b.Body.position+Vector3.up*.6f);
                    Check(b.Hp==0&&!b.Seat.alive&&b.Death!=null,"fatal headshot did not record death ("+state+")");
                    Check(b.Death.kneel==grounded,"headshot collapse selection ("+state+"): kneel="+b.Death.kneel);
                    Check(b.StunEnd==stunEnd,"fatal projectile changed stun ("+state+"): expected "+stunEnd+", got "+b.StunEnd);
                }
                Debug.Log("PASS native gun/magic primaries, release resources, swept capsule/cover, reload, reflection bounds, all live Q/E exact costs/cooldowns/charges, free casts at zero mana, paid cast refusal, multishot/split ledger and grounded/airborne headshot choreography with unchanged stun (four cases)");
            }
            finally{CapturedAnimation.Release(wall);Bullets.Clear();AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();}
        }
        public void ValidatePveEconomy(NativePveContent data)
        {
            Match.Seats.Add(new NativeSeat{id=8,name="Siren",spec="siren",team=7,AuthorityBoss=true,bot=true});Reset();ConfigurePve(data,false);
            if(Pve==null)throw new InvalidOperationException("Configure PvE before its acceptance gate");void Check(bool ok,string message){if(!ok)throw new Exception("Native PvE gate: "+message);}
            StartPve();var pilot=Members().First();var row=Pve.Rows[pilot.Seat.id];Check(row.Money==150&&row.Open!=null&&row.Open.Offers.Count>0,"opening money/draft");string chip=row.Open.Offers[0];int pick=row.Open.Id;Check(ChoosePve(pilot,pick,chip)&&pilot.Seat.chips[chip]==1&&!ChoosePve(pilot,pick,chip),"draft exactly once");
            var machine=Pve.Content.machines.First(m=>m.kind=="chip");pilot.Body.position=Vec(machine.front.Position(.9f));row.Money=1000;int cost=NativePveRules.ChipPrice(row.Bought),before=row.Money;PveInteract(pilot,row);Check(row.Money==before-cost&&Pve.Pickups.Count==1&&Pve.Machines.Last().result=="chip","paid dispenser");
            var pickup=Pve.Pickups[0];while(Time<pickup.Born+.001f)Match.Tick(.0167);pilot.Body.position=pickup.Position;PveInteract(pilot,row);Check(Pve.Pickups.Count==0&&row.Picks.Count==1,"pickup collection");OpenPvePick(pilot);ChoosePve(pilot,row.Open.Id,row.Open.Offers[0]);
            var med=Pve.Content.machines.First(m=>m.kind=="med");pilot.Body.position=Vec(med.front.Position(.9f));pilot.Hp=20;before=row.Money;PveInteract(pilot,row);Check(row.Money==before-75&&row.HealPulses==0&&row.HealReady>Time,"paid healing");before=row.Money;PveInteract(pilot,row);Check(row.Money==before&&Pve.Machines.Last().result=="deny","med cooldown charged twice");
            var recruit=Pve.Content.machines.First(m=>m.kind=="recruit");pilot.Body.position=Vec(recruit.front.Position(.9f));row.Money=1000;PveInteract(pilot,row);PveInteract(pilot,row);before=row.Money;PveInteract(pilot,row);Check(Pve.Hires.Count==2&&row.Money==before,"pending recruit cap");
            pilot.Hp=0;pilot.Seat.alive=false;pilot.Collider.enabled=false;pilot.Body.isKinematic=true;RevivePve(pilot);Check(pilot.Hp==(float)pilot.Stats["hp"]*.5f&&pilot.Collider.enabled&&!pilot.Body.isKinematic&&pilot.ShieldUntil>Time&&pilot.Wounds.Count==0,"safe revival state");
            ValidatePveMinionContacts(pilot);
            var copy=NativeFrameCodec.Clone(NetworkFrame());if(copy.pve.rows.First(r=>r.id==pilot.Seat.id).revived!=row.Revived)throw new Exception("PvE revive count lost in guest/replay frame");
            Debug.Log("PASS native PvE opening/personal picks, machine purchases and denial, pickup ownership, healing cooldown, pending recruit caps and revival state");
        }
        private void ValidatePveMinionContacts(NativePilot pilot)
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Native PvE contact gate: "+message);}
            Pve.Holding=false;SetBossHidden(true);var add=SpawnPveAdd("shambler",true);Check(add!=null,"spawn fixture");
            add.Body.position=new Vector3(20,10,20);add.Collider.enabled=true;add.Pve.ActiveAt=add.Pve.ExposedAt=Time-1;add.Pve.Travelling=false;add.Hp=add.MaxHp=1000;
            pilot.Body.position=add.Body.position-Vector3.forward*2;pilot.Seat.connected=true;pilot.Seat.chips.Clear();pilot.Seat.spec="pistol";pilot.Weapon=Content.Weapon("pistol");pilot.Stats=NativeContentRules.Stats(Content,"pistol",pilot.Seat.chips);Physics.SyncTransforms();
            Check(!Pve.Boss.Seat.connected&&Enemy(pilot,add)&&!CanHit(pilot,Pve.Boss),"hidden boss and target acquisition");
            float hp=add.Hp;Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=pilot.Seat.id,Original=pilot.Seat.id,Spec="pistol",Position=add.Body.position-Vector3.forward,Velocity=Vector3.forward*120,Radius=.045f,Damage=10,Life=1,Travel=10,Direct=true});
            StepBullets();Check(add.Hp<hp,"swept projectile passed through the hidden boss's add");
            hp=add.Hp;Check(HitMinion(add,pilot,5,Vector3.forward,0)>0&&add.Hp<hp,"melee contact rejected");
            hp=add.Hp;AbilityBlast(pilot,add.Body.position,3,5,0,0,effect:"frag");Check(add.Hp<hp,"ability blast rejected");
            hp=add.Hp;ChipBlast(pilot,add.Body.position,3,5,0,effect:"block-pulse");Check(add.Hp<hp,"chip blast rejected");
            Pve.Holding=true;hp=add.Hp;Check(HitMinion(add,pilot,5,Vector3.forward,0)==0&&add.Hp==hp,"cinematic protection lost");Pve.Holding=false;
            add.Pve.Travelling=true;Check(HitMinion(add,pilot,5,Vector3.forward,0)==0,"vent travel protection lost");add.Pve.Travelling=false;
            add.Pve.ExposedAt=Time+1;Check(HitMinion(add,pilot,5,Vector3.forward,0)==0,"unexposed entry protection lost");add.Pve.ExposedAt=Time-1;
            Events.Clear();HitMinion(add,pilot,1,Vector3.right,0);var hit=Events.Last(e=>e.Kind==EventKind.Hit);Check(hit.Normal.x==1&&hit.Normal.y==0&&hit.Detail=="direct","directed add hit payload");
            Events.Clear();HitMinion(add,pilot,1,Vector3.zero,0,null,"dot");Check(Events.Last(e=>e.Kind==EventKind.Hit).Normal.Length==0,"DoT generated a directed splatter");
            RemoveMinion(add,false);Bullets.Clear();
            Debug.Log("PASS hidden Siren adds: projectile sweep, melee, ability/chip blast, target acquisition, hold/entry/travel protections and hit direction");
        }
    }
}
#endif
