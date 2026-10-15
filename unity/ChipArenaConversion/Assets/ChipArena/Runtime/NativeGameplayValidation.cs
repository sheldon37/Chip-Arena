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
                wall.SetActive(false);Bullets.Clear();shotEnds.Clear();AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();MeleeShots.Clear();abilityEchoes.Clear();ChipFields.Clear();Match.Teams=false;
                foreach(var p in new[]{a,b}){CancelSiren(p);CancelFighter(p);CancelSpecial(p);CancelMelee(p);CancelAbilities(p);p.Seat.chips.Clear();p.Seat.spec=p==a?spec:"sword";p.Seat.bot=false;p.Seat.alive=p.Seat.connected=true;p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);p.Hp=p.Mana=p.Stamina=100;p.Body.isKinematic=false;p.Body.position=new Vector3(1000,10,p==a?1000:1002);p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;p.Collider.enabled=true;p.Down=null;p.Juggle=null;p.Roll=null;p.Death=null;p.Wounds.Clear();p.Dots.Clear();p.BowDrawAt=-1;p.BowRainAt=-100;p.BowRainNext=0;p.OverdriveAt=p.OverdriveUntil=p.VentUntil=0;p.Ready=p.QReady=p.EReady=p.ReloadEnd=p.BlockEnd=p.RollEnd=p.StunEnd=p.FrozenUntil=p.ScareUntil=p.ShieldUntil=p.CastAt=p.AttackEnd=0;p.ECharges=1;p.Ammo=Magazine(p);p.Yaw=p==a?0:180;p.Pitch=0;p.Input=new InputFrame{Yaw=p.Yaw,AimTarget=Vec(b.Body.position)};p.RegenAt=Time+20;}
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
            void CheckCyberAbility(ContentAbility ability)
            {
                // Kernel Panic has its own native connect/whiff/isolation gate in CheckCombatParity.
                if(ability.slot=="4"){Check(ability.id=="kernel-panic","unknown Cyber Lord ultimate");return;}
                Check(new[]{"data-dash","firewall","packet-burst","siphon-link","system-crash"}.Contains(ability.id),"unhandled Cyber Lord ability "+ability.id);
                var savedAccess=a.Seat.access;
                var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
                int kitSlot=ability.slot=="q"?0:ability.slot=="e"?1:ability.slot=="1"?3:ability.slot=="2"?4:5;
                float Ready()=>Math.Max(0,a.Cyber.Kit2?.Ready[kitSlot]??0);
                void Prepare(NativeAccess access,float mana)
                {
                    Setup(CyberLordSpec.Id);a.Seat.access=access;a.Cyber=new NativeCyberKit();a.Kernel=null;a.KernelPinned=false;
                    a.CyberTrailTick=a.CyberWallTick=a.CyberLinkTick=0;a.Mana=mana;a.ECharges=0;
                    a.Stats["resourceCost"]=.8;a.Stats["abilityCost"]=.9;a.Stats["abilityCooldown"]=1.3;
                    a.Input.AbilityQ=ability.slot=="q";a.Input.AbilityE=ability.slot=="e";a.Input.SirenAbility=(byte)(ability.slot=="1"?1:ability.slot=="2"?2:3);
                    if(ability.slot=="q"||ability.slot=="e")a.Input.SirenAbility=0;
                }
                try
                {
                    Prepare(NativeAccess.Guest,100);StepAbilities(a);
                    Check(a.Cyber.Kind==""&&a.AbilityCast==null&&a.Mana==100&&Ready()==0,"Cyber Lord DEV refusal "+ability.id);
                    Prepare(grant,0);StepAbilities(a);
                    Check(a.Cyber.Kind==""&&a.AbilityCast==null&&a.Mana==0&&Ready()==0,"Cyber Lord unfunded refusal "+ability.id);
                    Prepare(grant,100);float at=Time;StepAbilities(a);
                    float expectedMana=100-CyberKit2Spec.Costs[kitSlot]*(float)(a.Stats["resourceCost"]*a.Stats["abilityCost"]);
                    Check(a.Cyber.Version==2&&a.Cyber.Kind==CyberKit2Spec.Ids[kitSlot]&&a.Cyber.At==at&&a.AbilityCast==null,"Cyber v2 input dispatch "+ability.slot);
                    Check(Math.Abs(a.Mana-expectedMana)<.001f&&a.ECharges==0,"Cyber v2 cost/no E charges "+ability.slot);
                    Check(Math.Abs(Ready()-(at+CyberKit2Spec.Cooldowns[kitSlot]*a.Stats["abilityCooldown"]))<.001f,"Cyber v2 cooldown "+ability.slot);
                    Check(Math.Abs(a.Cyber.Release-(at+CyberKit2Spec.Windup[kitSlot]))<.001f,"Cyber v2 release "+ability.slot);
                    Check(Math.Abs(a.Cyber.Until-(at+CyberKit2Spec.Seconds[kitSlot]))<.001f,"Cyber v2 duration "+ability.slot);
                    float ready=Ready();Advance(a.Cyber.Until-Time+.01f);StepAbilities(a);Check(Ready()==ready,"Cyber v2 cooldown refusal "+ability.slot);
                }
                finally{a.Seat.access=savedAccess;a.Cyber=new NativeCyberKit();a.CyberTrailTick=a.CyberWallTick=a.CyberLinkTick=0;}
            }
            void CheckBowAbilityRelease(ContentAbility ability)
            {
                Check(ability.id=="volley"||ability.id=="rain-of-arrows","unhandled Bow ability "+ability.id);
                Setup("bow");a.Mana=0;a.Ammo=0;a.ECharges=0;
                a.Input.AbilityQ=ability.slot=="q";a.Input.AbilityE=ability.slot=="e";
                float at=Time,scale=(float)a.Stats["abilityCooldown"];
                // Bow Q/E commit through input, without generic windup, mana or E charges.
                StepAbilities(a);
                Check(a.AbilityCast==null&&a.Pose.AbilityId==ability.id,"Bow input dispatch "+ability.id);
                float ready=ability.slot=="q"?a.QReady:a.EReady;
                Check(Math.Abs(ready-(at+(ability.slot=="q"?8:12)*scale))<.001f,"Bow cooldown "+ability.id);
                Check(Math.Abs(a.Ready-(at+NativeBowRules.Nock))<.001f,"Bow nock recovery "+ability.id);
                if(ability.slot=="q")
                    Check(Bullets.Count==5&&Bullets.All(x=>x.Detail=="volley"),"Bow Volley releases five arrows");
                else
                {
                    Check(Bullets.Count==0&&a.BowRainAt==at&&a.BowRainNext==0,"Bow Rain telegraph before arrows");
                    Advance(.5f);StepBow(a);Check(Bullets.Count==0,"Bow Rain must wait .6 seconds");
                    Advance(.11f);StepBow(a);Check(Bullets.Count==1&&a.BowRainNext==1,"Bow Rain first arrow");
                    Advance(1.2f);StepBow(a);
                    Check(Bullets.Count==12&&a.BowRainNext==12&&Bullets.All(x=>x.Detail=="rain"&&x.Velocity.y<0),"Bow Rain catches up to twelve downward arrows");
                    StepBow(a);Check(Bullets.Count==12,"Bow Rain cannot duplicate arrows");
                }
                int count=Bullets.Count;StepAbilities(a);
                Check(Bullets.Count==count&&(ability.slot=="q"?a.QReady:a.EReady)==ready,"Bow cooldown blocks repeat "+ability.id);
                Check(a.Mana==0&&a.Ammo==0&&a.ECharges==0,"Bow abilities do not consume mana/ammo/charges "+ability.id);
            }
            try
            {
                foreach(string spec in new[]{"pistol","rifle","machinegun","fire","water","lightning","darkness"})
                {
                    var shot=Shot(spec);Check(a.Weapon.@class=="magic"?a.Mana<100:a.Ammo==Magazine(a)-1,"resource debit "+spec);shot.Position=new Vector3(1000,10,1000.8f);shot.Velocity=Vector3.forward*120;shot.Gravity=0;StepBullets();Check(b.Hp<100,"swept primary missed "+spec);var endpoint=NetworkFrame().projectileEnds;Check(endpoint.Length==1&&endpoint[0].Id==shot.Id&&endpoint[0].At==Time&&(Vec(endpoint[0].Position)-shot.Position).sqrMagnitude<.000001f,"recorded primary impact "+spec);float hp=b.Hp;StepBullets();Check(b.Hp==hp,"duplicate impact "+spec);
                    shot=Shot(spec);shot.Position=new Vector3(1000,10,1000.8f);shot.Velocity=Vector3.forward*120;shot.Gravity=0;wall.SetActive(true);Physics.SyncTransforms();StepBullets();Check(b.Hp==100,"primary passed through cover "+spec);Check(NetworkFrame().projectileEnds.Any(e=>e.Id==shot.Id),"recorded cover impact "+spec);
                }
                Setup("rifle");a.Ammo=0;a.Input.Shoot=true;StepPilot(a);Check(Bullets.Count==0&&a.ReloadEnd>Time,"empty rifle did not reload");a.Input.Shoot=false;Advance(a.ReloadEnd-Time+.001f);StepPilot(a);Check(a.Ammo==Magazine(a)&&a.ReloadEnd==0,"reload completion");
                var reflected=Shot("pistol");b.BlockEnd=Time+1;b.Stats["reflectSpeed"]=10;b.Stats["reflectDamage"]=10;reflected.Position=b.Body.position;reflected.Velocity=Vector3.forward*240;float life=reflected.Life;StepBullets();Check(Bullets.Contains(reflected)&&reflected.Owner==b.Seat.id&&reflected.Velocity.z<0&&reflected.Damage<=100&&reflected.Velocity.magnitude<=120&&reflected.Life<life,"block reflection/limits");Check(!NetworkFrame().projectileEnds.Any(e=>e.Id==reflected.Id),"reflection must not end the replay shot");
                ValidateRangedQ(Setup,Advance,a,b,wall);
                foreach(var ability in Content.abilities.Where(d=>d.live&&d.kind!="ads"&&!NativeRangedRules.Q(d.id)))
                {
                    var weapon=Content.weapons.FirstOrDefault(w=>Content.Ability(w.id,ability.slot)?.id==ability.id);if(weapon==null)throw new Exception("Unmapped live ability: "+ability.id);
                    // Standard melee uses Special and pays stamina at commitment. Its Q/E
                    // dispatcher/release contract is checked by ValidateSpecials, not AbilityCast.
                    if(NativeMeleeRules.Standard(weapon.id))continue;
                    if(weapon.id=="bow"){CheckBowAbilityRelease(ability);continue;}
                    if(weapon.id==CyberLordSpec.Id){CheckCyberAbility(ability);continue;}
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
                Debug.Log("PASS native gun/magic primaries, release resources, swept capsule/cover, reload, reflection bounds, all live gun/magic Q/E exact costs/cooldowns/charges, Cyber Lord native input/DEV/cost/cooldown contracts (Kernel Panic checked separately), free casts at zero mana, paid cast refusal, multishot/split ledger and grounded/airborne headshot choreography with unchanged stun (four cases)");
            }
            finally{CapturedAnimation.Release(wall);Bullets.Clear();AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();}
        }
        public void ValidatePveEconomy(NativePveContent data)
        {
            Match.Seats.Add(new NativeSeat{id=8,name="Siren",spec="siren",team=7,AuthorityBoss=true,bot=true});Reset();ConfigurePve(data,false);
            if(Pve==null)throw new InvalidOperationException("Configure PvE before its acceptance gate");void Check(bool ok,string message){if(!ok)throw new Exception("Native PvE gate: "+message);}
            StartPve();var pilot=Members().First();var row=Pve.Rows[pilot.Seat.id];Check(row.Money==150&&row.Open!=null&&row.Open.Offers.Count==3,"opening money/three-choice draft");string chip=row.Open.Offers[0];int pick=row.Open.Id;Check(ChoosePve(pilot,pick,chip)&&pilot.Seat.chips[chip]==1&&!ChoosePve(pilot,pick,chip),"draft exactly once");
            var machine=Pve.Content.machines.First(m=>m.kind=="chip");pilot.Body.position=Vec(machine.front.Position(.9f));row.Money=1000;int cost=NativePveRules.ChipPrice(row.Bought),before=row.Money;PveInteract(pilot,row);Check(row.Money==before-cost&&Pve.Pickups.Count==1&&Pve.Machines.Last().result=="chip","paid dispenser");
            ValidatePvePickups(pilot);OpenPvePick(pilot);Check(row.Open.Offers.Count==3,"collected chip has three choices");ChoosePve(pilot,row.Open.Id,row.Open.Offers[0]);
            var med=Pve.Content.machines.First(m=>m.kind=="med");pilot.Body.position=Vec(med.front.Position(.9f));pilot.Hp=20;before=row.Money;PveInteract(pilot,row);Check(row.Money==before-75&&row.HealPulses==0&&row.HealReady>Time,"paid healing");before=row.Money;PveInteract(pilot,row);Check(row.Money==before&&Pve.Machines.Last().result=="deny","med cooldown charged twice");
            var recruit=Pve.Content.machines.First(m=>m.kind=="recruit");pilot.Body.position=Vec(recruit.front.Position(.9f));row.Money=1000;PveInteract(pilot,row);PveInteract(pilot,row);before=row.Money;PveInteract(pilot,row);Check(Pve.Hires.Count==2&&row.Money==before,"pending recruit cap");
            pilot.Hp=0;pilot.Seat.alive=false;pilot.Collider.enabled=false;pilot.Body.isKinematic=true;RevivePve(pilot);Check(pilot.Hp==(float)pilot.Stats["hp"]*.5f&&pilot.Collider.enabled&&!pilot.Body.isKinematic&&pilot.ShieldUntil>Time&&pilot.Wounds.Count==0,"safe revival state");
            ValidatePveMinionContacts(pilot);
            var copy=NativeFrameCodec.Clone(NetworkFrame());if(copy.pve.rows.First(r=>r.id==pilot.Seat.id).revived!=row.Revived)throw new Exception("PvE revive count lost in guest/replay frame");
            Debug.Log("PASS native PvE opening/personal picks, machine purchases and denial, pickup ownership, healing cooldown, pending recruit caps and revival state");
            ValidatePveBotCombat();
            // The held PvE entrance advances its song while match time is frozen, then transfers the full intro duration
            // to BossSongLead. Exercise the real start/tick path and the guest's actual frame application.
            var guest=new NativeArena(Content,new NativeMatch(Content),MapId){Replica=true,ReplicaScene=root.scene};
            try
            {
                var order=new NativeFrameOrder();
                void Deliver()
                {
                    var packet=NativeFrameBudget.Encode(NetworkFrame(),7);Check(packet!=null,"guest presentation packet fits budget");
                    var msg=NativeNetCodec.Decode(NativeConnection.Decode(packet,NativeFrameBudget.MaxJsonBytes));var f=msg.frame;
                    Check(f.Valid(Content)&&order.Frame(msg.epoch,f.generation,f.time,f.sequence),"fresh guest presentation frame accepted while game clock is frozen");
                    guest.ApplyFrame(f,LocalId);Check(guest.Pve.Outcome==null,"unfinished guest PvE is not an empty-string outcome");
                }
                BeginPveHold("reveal",1);float started=Time;Deliver();Check(guest.Pve.Cinematic=="reveal"&&guest.Pve.Holding,"guest sees reveal");
                PveHoldTick(Pve.HoldLeft+.001f);Deliver();Check(Time==started&&guest.Pve.Cinematic=="intro"&&guest.Pve.Holding,"guest sees reveal-to-intro transition at same timestamp");
                Check(IntroStartsBossSong&&Math.Abs(BossSongTime)<.0001,"intro/song start together");
                PveHoldTick(.65f);Deliver();Check(Time==started&&IntroStartsBossSong&&Math.Abs(BossSongTime-.65)<.0001,"held intro/song age");
                Check(guest.IntroStartsBossSong&&Math.Abs(guest.BossSongTime-BossSongTime)<.0001,"guest held intro/song age");
                PveHoldTick(Pve.HoldLeft+.001f);Check(!IntroStartsBossSong&&Math.Abs(BossSongTime-Pve.HoldDuration)<.0001,"song continues after intro");
                Deliver();
                Check(!guest.IntroStartsBossSong&&Math.Abs(guest.BossSongTime-BossSongTime)<.0001,"guest song continues after intro");
                // Personal chip UI is driven by the requesting seat's open pick and all its offers, never another row.
                GrantPvePick(Local,"common",true);Deliver();var open=Pve.Rows[LocalId].Open;var remote=guest.Pve.Rows[LocalId].Open;
                Check(remote!=null&&remote.Id==open.Id&&remote.Offers.SequenceEqual(open.Offers)&&remote.Offers.All(id=>guest.PveChip(id)!=null),"complete guest personal chip screen");
                ClosePvePick(Local);Deliver();Check(guest.Pve.Rows[LocalId].Open==null,"guest choose-later clears personal screen at the same timestamp");
                Check(OpenPvePick(Local,open.Id),"reopen guest fixture");Deliver();Check(guest.Pve.Rows[LocalId].Open?.Id==open.Id,"guest reopen at the same timestamp");
                var late=new NativeArena(Content,new NativeMatch(Content),MapId){Replica=true,ReplicaScene=root.scene};
                try{late.ApplyFrame(NativeFrameCodec.Clone(NetworkFrame()),LocalId);Check(late.Pve.Rows[LocalId].Open.Offers.SequenceEqual(open.Offers),"late guest recovers open personal screen");}finally{late.Dispose();}
                ValidatePossessionReplication(guest);
            }
            finally{guest.Dispose();}
            Debug.Log("PASS Siren guest screens/audio: real reveal/intro/start/tick/finish traverse framed codec/order/apply at frozen time; personal picks, choose-later/reopen and late join retain all offers");
        }
        private void ValidatePvePickups(NativePilot pilot)
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Native PvE pickup gate: "+message);}
            var row=Pve.Rows[pilot.Seat.id];var pickup=Pve.Pickups.Single();pilot.Input=default;pilot.Body.position=pickup.Position;
            PveInput(pilot);Check(Pve.Pickups.Count==1&&row.Picks.Count==0,"dispense delay");
            while(Time<pickup.Born+.001f)Match.Tick(.0167);
            var other=Members().First(p=>p!=pilot);var otherRow=Pve.Rows[other.Seat.id];int otherPicks=otherRow.Picks.Count;var otherPosition=other.Body.position;
            other.Body.position=pickup.Position;other.Input=default;PveInput(other);other.Body.position=otherPosition;
            Check(Pve.Pickups.Count==1&&otherRow.Picks.Count==otherPicks,"non-owner cannot take a chip");
            pilot.Body.position=pickup.Position+Vector3.right*1.7f;PveInput(pilot);Check(Pve.Pickups.Count==1,"outside pickup radius");
            pilot.Body.position=pickup.Position+Vector3.up*3;PveInput(pilot);Check(Pve.Pickups.Count==1,"different floor");pilot.Body.position=pickup.Position;
            float hp=pilot.Hp;pilot.Hp=0;PveInput(pilot);pilot.Hp=hp;Check(Pve.Pickups.Count==1,"downed owner");
            pilot.Seat.connected=false;PveInput(pilot);pilot.Seat.connected=true;Check(Pve.Pickups.Count==1,"disconnected owner");
            Replica=true;PveInput(pilot);Replica=false;Check(Pve.Pickups.Count==1,"replica cannot collect");
            Pve.Holding=true;PveInput(pilot);Pve.Holding=false;Check(Pve.Pickups.Count==1,"cinematic hold");
            int money=row.Money;PveInput(pilot);PveInput(pilot);
            Check(Pve.Pickups.Count==0&&row.Picks.Count==1&&row.Open==null&&row.Money==money,"walk-over queues exactly once without F or another charge");
            var frame=NativeFrameCodec.Clone(NetworkFrame());var ownerFrame=frame.pve.rows.Single(r=>r.id==pilot.Seat.id);
            Check(frame.pve.pickups.Length==0&&ownerFrame.picks.Length==1&&ownerFrame.picks[0].source=="machine"&&frame.pve.rows.Single(r=>r.id==other.Seat.id).picks.Length==otherPicks,"guest frame preserves personal collection");
            int choices=Match.Choices;
            try
            {
                foreach(int setting in new[]{1,6,12})foreach(var tier in Pve.Content.offerWeights)
                {Match.Choices=setting;var pick=new NativePvePick{Tier=tier.id};RollPveOffers(pilot,pick);Check(pick.Offers.Count==3&&pick.Offers.Distinct().Count()==3,"three distinct "+tier.id+" choices with PvP setting "+setting);}
            }
            finally{Match.Choices=choices;}
            Debug.Log("PASS native PvE owner-only walk-over pickups, dispense delay, alive/connected/height/authority guards, queued guest frame and three choices in every tier");
        }
        private void ValidatePossessionReplication(NativeArena guest)
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Possession replication: "+message);}
            var add=SpawnPveAdd("shambler",true);Check(add!=null,"spawn fixture");
            try
            {
                add.Pve.Possessed=true;add.Pve.PossessedAt=Time;float started=Time;
                // Late observation must retain the host's start, rather than restart the hints locally.
                for(int tick=0;tick<144;tick++)Match.Tick(.25);
                var frame=NetworkFrame();Check(frame.Valid(Content),"authority frame invalid");
                var wire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=frame})).frame;
                var replay=NativeFrameCodec.Clone(wire);
                foreach(var observed in new[]{wire,replay})
                {
                    Check(observed.Valid(Content),"codec frame invalid");guest.ApplyFrame(observed,LocalId);
                    var remote=guest.Minions.Single(m=>m.Id==add.Id);
                    Check(remote.Pve!=null&&remote.Pve.Possessed&&remote.Pve.PossessedAt==started,"guest/replay lost possession start");
                    Check(Math.Abs((guest.Time-remote.Pve.PossessedAt)-(Time-started))<.0001,"late guest possession age differs");
                }
                foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f})
                {
                    var bad=NativeFrameCodec.Clone(frame);bad.minions.Single(m=>m.id==add.Id).possessedAt=invalid;
                    Check(!bad.Valid(Content),"invalid possession timestamp accepted");
                }
                add.Pve.Possessed=false;add.Pve.PossessedAt=0;
                guest.ApplyFrame(NativeFrameCodec.Clone(NetworkFrame()),LocalId);
                var cleared=guest.Minions.Single(m=>m.Id==add.Id).Pve;
                Check(!cleared.Possessed&&cleared.PossessedAt==0,"stale possession survived next frame");
                Check(JsonUtility.FromJson<NativeMinionFrame>("{\"possessed\":true}").possessedAt==0,"older frame default changed");
                Debug.Log("PASS possession timestamp: authority capture, real network codec, replay clone, late guest age, clearing and invalid timestamp rejection");
            }
            finally{RemoveMinion(add,false);}
        }
        private void ValidatePveBotCombat()
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Native PvE bot gate: "+message);}
            bool Attacks(InputFrame i)=>i.Shoot||i.AbilityQ||i.AbilityE||i.Hadoken||i.Uppercut||i.SirenAbility>0;
            var human=Members().First();var bot=Members().First(p=>p!=human);var state=Pve;
            bool stationary=StationaryBots;Pve.Holding=false;Pve.Hires.Clear();
            human.Seat.bot=false;human.Seat.team=bot.Seat.team=0;human.Body.position=new Vector3(7,20,0);
            human.Collider.enabled=true;human.Hp=100;human.Seat.connected=true;
            var add=SpawnPveAdd("shambler",true);Check(add!=null,"spawn target");
            add.Body.position=new Vector3(0,20,2);add.Collider.enabled=true;add.Hp=add.MaxHp=10000;
            add.Pve.ActiveAt=add.Pve.ExposedAt=Time-1;add.Pve.Travelling=false;add.Pve.Target=human;
            void Setup(string spec,bool hidden,bool stationarySetting=false,string difficulty="hard")
            {
                Bullets.Clear();AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();MeleeShots.Clear();
                CancelSiren(bot);CancelFighter(bot);CancelSpecial(bot);CancelMelee(bot);CancelAbilities(bot);
                bot.Seat.bot=bot.Seat.connected=bot.Seat.alive=true;bot.Seat.spec=spec;bot.Seat.difficulty=difficulty;bot.Seat.chips.Clear();
                bot.Weapon=Content.Weapon(spec);bot.Stats=NativeContentRules.Stats(Content,spec,bot.Seat.chips);
                bot.Hp=bot.Mana=bot.Stamina=100;bot.Body.isKinematic=false;bot.Body.position=new Vector3(0,20,0);
                bot.Body.linearVelocity=bot.Knockback=bot.MoveVelocity=Vector3.zero;bot.Collider.enabled=true;
                bot.Down=null;bot.Juggle=null;bot.Roll=null;bot.Death=null;bot.Wounds.Clear();bot.Dots.Clear();
                bot.Ready=bot.ReloadEnd=bot.BlockEnd=bot.RollEnd=bot.StunEnd=bot.FrozenUntil=bot.ScareUntil=bot.ShieldUntil=bot.CastAt=bot.AttackEnd=0;
                // Hold specials on cooldown so the assertion exercises primary weapon releases.
                bot.QReady=bot.EReady=Time+100;bot.ECharges=0;bot.Ammo=Magazine(bot);bot.Grounded=true;
                bot.BotBrain.ThinkAt=0;bot.BotBrain.Aim=new V3();bot.BotBlocked=0;bot.Yaw=bot.Pitch=0;
                var row=Pve.Rows[bot.Seat.id];row.Open=null;row.Picks.Clear();
                StationaryBots=stationarySetting;SetBossHidden(hidden);Pve.Boss.Hp=10000;
                if(!hidden)Pve.Boss.Body.position=new Vector3(0,20,-8);
                Physics.SyncTransforms();
            }
            try
            {
                // Exercise the same single entry point used by Step, with the boss on the
                // opposite side of the bot. The former nested brains aimed at the boss here.
                foreach(bool hidden in new[]{true,false})foreach(string spec in new[]{"pistol","fire","sword"})
                {
                    Setup(spec,hidden);bot.Input=Bot(bot);PveInput(bot);
                    Check(bot.Input.Shoot&&bot.Input.AimTarget.z>1&&Math.Abs(bot.Input.AimTarget.x)<1,"primary/aim "+spec+", hidden="+hidden);
                    float hp=add.Hp;
                    for(int tick=0;tick<180&&add.Hp==hp;tick++)
                    {
                        bot.Input=Bot(bot);PveInput(bot);StepPilot(bot);StepBullets();Match.Tick(1d/60);
                    }
                    Check(add.Hp<hp,"bot primary did not damage minion: "+spec+", hidden="+hidden);
                }
                foreach(bool held in new[]{false,true})foreach(string difficulty in new[]{"normal","dummy"})
                {
                    Setup("pistol",true,held,difficulty);Check(Bot(bot).Shoot,"PvP practice setting silenced PvE fighter");
                }
                Setup("pistol",true);Pve.Recruits.Add(new NativePveRecruit{Pilot=bot,Owner=human,Role="dps"});
                Check(Bot(bot).Shoot,"DPS recruit did not attack");Pve.Recruits.Last().Role="tank";
                Check(Bot(bot).Shoot,"guardian did not attack");Pve.Recruits.Last().Role="medic";
                Check(!Attacks(Bot(bot)),"medic attacked");Pve.Recruits.RemoveAll(r=>r.Pilot==bot);
                add.Pve.ExposedAt=Time+10;Setup("pistol",true);Check(!Attacks(Bot(bot)),"unexposed add targeted");
                add.Pve.ExposedAt=Time-1;add.Hp=0;Setup("pistol",true);Check(!Attacks(Bot(bot)),"dead add or hidden boss targeted");
                Setup("pistol",false,true,"dummy");var bossInput=Bot(bot);
                Check(bossInput.Shoot&&bossInput.AimTarget.z<0,"visible boss not attacked with inherited practice settings");
                Pve=null;Match.Teams=false;
                StationaryBots=true;bot.Seat.difficulty="normal";Check(!Attacks(Bot(bot)),"PvP stationary setting lost");
                StationaryBots=false;bot.Seat.difficulty="dummy";Check(!Attacks(Bot(bot)),"PvP dummy setting lost");
                Debug.Log("PASS native PvE allied bots: gun/magic/melee minion damage with hidden/visible Siren, single-target aim, inherited practice settings, recruits/medics and PvP dummy preservation");
            }
            finally{Pve=state;Match.Teams=true;StationaryBots=stationary;Pve.Recruits.RemoveAll(r=>r.Pilot==bot);RemoveMinion(add,false);Bullets.Clear();AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();MeleeShots.Clear();}
        }
        private void ValidatePveMinionContacts(NativePilot pilot)
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Native PvE contact gate: "+message);}
            var pilotAt=pilot.Body.position;Pve.Holding=false;SetBossHidden(true);var add=SpawnPveAdd("shambler",true);Check(add!=null,"spawn fixture");
            // Use the isolated projectile-test lane; the old (20,10,19) muzzle is inside the revamped concourse wall.
            add.Body.position=new Vector3(1000,10,1002);add.Collider.enabled=true;add.Pve.ActiveAt=add.Pve.ExposedAt=Time-1;add.Pve.Travelling=false;add.Hp=add.MaxHp=1000;
            pilot.Body.position=add.Body.position-Vector3.forward*2;pilot.Seat.connected=true;pilot.Seat.chips.Clear();pilot.Seat.spec="pistol";pilot.Weapon=Content.Weapon("pistol");pilot.Stats=NativeContentRules.Stats(Content,"pistol",pilot.Seat.chips);Physics.SyncTransforms();
            Check(!Pve.Boss.Seat.connected&&Enemy(pilot,add)&&!CanHit(pilot,Pve.Boss),"hidden boss and target acquisition");
            var contact=SweepNative(add.Body.position-Vector3.forward,Vector3.forward*2,.045f,c=>c!=pilot.Collider);
            Check(contact.HasValue&&contact.Value.Collider==add.Collider,"fixture sweep obstructed by "+(contact.HasValue?contact.Value.Collider.name:"no collider"));
            float hp=add.Hp;Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=pilot.Seat.id,Original=pilot.Seat.id,Spec="pistol",Position=add.Body.position-Vector3.forward,Velocity=Vector3.forward*120,Radius=.045f,Damage=10,Life=1,Travel=10,Direct=true});
            StepBullets();Check(add.Hp<hp,"swept projectile passed through the hidden boss's add");
            hp=add.Hp;Check(HitMinion(add,pilot,5,Vector3.forward,0)>0&&add.Hp<hp,"melee contact rejected");
            hp=add.Hp;AbilityBlast(pilot,add.Body.position,3,5,0,0,effect:"frag");Check(add.Hp<hp,"ability blast rejected");
            hp=add.Hp;ChipBlast(pilot,add.Body.position,3,5,0,effect:"block-pulse");Check(add.Hp<hp,"chip blast rejected");
            Pve.Holding=true;hp=add.Hp;Check(HitMinion(add,pilot,5,Vector3.forward,0)==0&&add.Hp==hp,"cinematic protection lost");Pve.Holding=false;
            add.Pve.Travelling=true;Check(HitMinion(add,pilot,5,Vector3.forward,0)==0,"vent travel protection lost");add.Pve.Travelling=false;
            add.Pve.ExposedAt=Time+1;Check(HitMinion(add,pilot,5,Vector3.forward,0)==0,"unexposed entry protection lost");add.Pve.ExposedAt=Time-1;
            Events.Clear();HitMinion(add,pilot,1,Vector3.right,0);var hit=Events.Last(e=>e.Kind==EventKind.Hit);Check(hit.Normal.x==1&&hit.Normal.y==0&&hit.Detail=="direct","directed add hit payload");Check(Events.Any(e=>e.Kind==EventKind.Impact&&e.Target==add.Id&&e.Source==pilot.Seat.id),"add contact impact missing");
            Events.Clear();HitMinion(add,pilot,1,Vector3.zero,0,null,"dot");Check(Events.Last(e=>e.Kind==EventKind.Hit).Normal.Length==0,"DoT generated a directed splatter");Check(!Events.Any(e=>e.Kind==EventKind.Impact),"add DoT emitted contact impact");
            RemoveMinion(add,false);Bullets.Clear();pilot.Body.position=pilotAt;Physics.SyncTransforms();
            Debug.Log("PASS hidden Siren adds: projectile sweep, melee, ability/chip blast, target acquisition, hold/entry/travel protections and hit direction");
        }
    }
}
#endif
