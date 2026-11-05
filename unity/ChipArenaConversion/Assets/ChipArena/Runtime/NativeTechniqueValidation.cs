#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateTechniques()
        {
            var oldBounds=Map.bounds;Map.bounds=new MapBounds{minX=950,maxX=1050,minZ=950,maxZ=1050,ceiling=200,killY=-6};var a=Pilots[0];var b=Pilots[1];int checks=0;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.layer=PropLayer;floor.transform.position=new Vector3(1000,98.635f,1000);floor.transform.localScale=new Vector3(80,1,80);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.layer=PropLayer;wall.transform.position=new Vector3(1000,103,1010);wall.transform.localScale=new Vector3(10,10,.2f);wall.SetActive(false);
            void Check(bool ok,string what){if(!ok)throw new Exception("Native techniques: "+what);checks++;}
            void Advance(float dt){Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=Match.Time+dt,fightAt=0});}
            void Setup(string spec)
            {
                wall.SetActive(false);Match.Teams=false;Match.Cooperative=false;Bullets.Clear();AbilityShots.Clear();MeleeShots.Clear();Events.Clear();
                foreach(var p in new[]{a,b})
                {
                    p.Seat.spec=p==a?spec:"sword";p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,new Dictionary<string,int>());p.Seat.chips.Clear();p.Seat.bot=false;p.Seat.connected=p.Seat.alive=true;
                    p.Hp=p.Stamina=p.Mana=100;p.Ammo=100;p.Body.isKinematic=false;p.Collider.enabled=true;p.Body.position=new Vector3(1000,100,p==a?1000:1005);p.Body.linearVelocity=p.MoveVelocity=p.Knockback=Vector3.zero;
                    p.Seat.weaponTechniques=NativeTechniqueLoadout.With("",p.Seat.spec,"e");p.QReady=p.EReady=0;p.Technique=null;p.TechniqueReleased=false;p.Special=null;p.Swing=null;p.MeleeKit=new NativeMeleeKit();p.Down=null;p.Juggle=null;p.Roll=null;p.HeldBy=p.Controlling=p.ControlledBy=null;
                    p.Ready=p.AttackEnd=p.BlockEnd=p.BlockReady=p.RollEnd=p.RollReady=p.StunEnd=p.FrozenUntil=p.ScareUntil=p.ShieldUntil=p.ReloadEnd=p.CastAt=p.LaunchUntil=p.StaggerImmune=0;p.SlowFactor=1;p.SlowEnd=0;p.Grounded=true;p.Dots.Clear();CancelAbilities(p);
                    p.TechniqueMarks=Array.Empty<NativeTechniqueMark>();p.ChipGauges=Array.Empty<NativeChipGauge>();p.ExpansionThiefAt=p.ExpansionDroneAt=-100;p.ExpansionDoomAt=-1;p.ExpansionStats=null;p.ExpansionStill=-1;p.ExpansionLastHit=-100;
                    p.Input=new InputFrame{Yaw=p==a?0:180,Pitch=0,AbilityTarget=new V3(1000,99.135f,1003)};p.Yaw=p.Input.Yaw;
                }
                Physics.SyncTransforms();
            }
            void Release(){Advance(a.Technique.Release-Time+.001f);a.Input.AbilityQ=a.Input.AbilityE=false;StepTechniqueInput(a);Check(a.TechniqueReleased,"release executes");}
            try
            {
                // Third-set foundation: real authority, clocks, Unity codec and replacement slots.
                foreach(string id in new[]{"gun-spin","overload","thunderclap"})
                {
                    var def=NativeTechniques.Find(id);Setup(def.Spec);a.Seat.weaponTechniques=NativeTechniqueLoadout.With("",def.Spec,"e",id);
                    a.Ammo=0;Check(TryTechnique(a)&&TechniqueBusy(a),"third windup locks: "+id);Release();
                    Check(a.Technique.Valid(),"third valid release: "+id);
                    if(id=="gun-spin")
                    {double rate=a.Stats["attackRate"];StepExpansionPilot(a);Check(a.Ammo==Magazine(a)&&Math.Abs(a.Stats["attackRate"]-rate*1.25)<.001,"gun spin refill/rate");Advance(4.1f);StepExpansionPilot(a);Check(Math.Abs(a.Stats["attackRate"]-rate)<.001,"gun spin rate expires");}
                    if(id=="overload")
                    {StepExpansionPilot(a);a.Hp=5;Advance(4.1f);StepTechniqueWorld();Check(a.Hp==1&&a.Technique.Phase=="shock","overload nonlethal shock");StepTechniqueWorld();Check(a.Hp==1,"shock once");}
                    if(id=="thunderclap")Check(b.Hp<100&&b.StunEnd>Time,"thunderclap damages and stuns");
                }
                Setup("lightning");a.Seat.weaponTechniques=NativeTechniqueLoadout.With("","lightning","e","overload");TryTechnique(a);Release();float earlyHp=a.Hp;Advance(.2f);Check(TryTechnique(a)&&a.Hp==earlyHp&&a.Technique.RecastAt==Time,"early overload recast avoids shock");
                Setup("pistol");a.Seat.weaponTechniques="";MarkTechnique(a,"disarm",b,2.5f);a.Input.AbilityQ=true;
                Check(TechniqueRefusal(a,in a.Input)==NativeTechniques.Disarmed,"default Q reports disarmed");StepAbilities(a);Check(a.Fan==null&&a.QReady==0,"disarm refuses default Q without charge");
                var markedFrame=NativeFrameCodec.Clone(NetworkFrame());var markedActor=markedFrame.snapshot.Actors.First(x=>x.Id==a.Seat.id);
                Check(markedActor.TechniqueMarks.Length==1&&markedActor.TechniqueMarks[0].By==b.Seat.id,"Unity codec victim marks");
                Setup("pistol");a.Seat.chips["wild-opening-move"]=1;a.Seat.chips["wild-hand-to-mouth"]=1;a.Ammo=0;
                float hit=Damage(b,a,10,Vector3.forward,0,false,primary:false);
                Check(Math.Abs(hit-15)<.001&&a.Ammo==1&&Events.Count(e=>e.Kind==EventKind.ChipProc&&e.Detail=="wild-hand-to-mouth")==1,"opening ability hit and ammo proc once");
                Check(Math.Abs(Damage(b,a,10,Vector3.forward,0,false,primary:false)-10)<.001,"opening charge spent");
                Advance(4.01f);StepExpansionPilot(a);Check(a.ChipGauges.Single().Value==1,"opening gauge rearms");
                Setup("pistol");a.Seat.chips["wild-mint-condition"]=1;a.Hp=(float)a.Stats["hp"];Check(Math.Abs(Damage(a,b,10,Vector3.back,0,false,primary:false)-4)<.001,"mint condition uses prehit health");
                void Third(string spec,string id){Setup(spec);a.Seat.weaponTechniques=NativeTechniqueLoadout.With("",spec,"e",id);}
                Third("machinegun","bipod");a.Ammo=0;Check(TryTechnique(a),"bipod casts empty");Release();StepExpansionPilot(a);
                Check(FireRound(a)&&a.Ammo==0,"bipod shoots without ammunition");var rooted=new Vector3(2,1,3);TechniqueMotion(a,ref rooted);Check(rooted.x==0&&rooted.z==0,"bipod roots planar motion");
                Check(TryTechnique(a)&&a.Technique.Phase=="packup"&&TechniqueBusy(a),"bipod recast packs for .3 s");
                Third("machinegun","bipod");a.Seat.chips["ab-belt-feed"]=1;a.Hp=50;TryTechnique(a);Release();Advance(1);StepTechniqueWorld();Check(Math.Abs(a.Hp-53)<.001&&Math.Abs(a.Technique.Until-a.Technique.Release-7)<.001,"belt feed duration and 3 HP/s");
                Third("darkness","soul-siphon");b.Body.position+=Vector3.right*40;Physics.SyncTransforms();Check(TechniqueRefusal(a,"e",in a.Input)==NativeTechniques.NoTarget&&!TryTechnique(a)&&a.Mana==100,"siphon target refusal before payment");
                b.Body.position=a.Body.position+Vector3.forward*5;Physics.SyncTransforms();a.Hp=50;TryTechnique(a);Release();Advance(.5f);StepTechniqueWorld();Check(b.Hp<100&&Math.Abs(a.Hp-50-(100-b.Hp))<.01,"siphon heals actual damage");
                var channelMotion=new Vector3(1,0,1);TechniqueMotion(a,ref channelMotion);Check(channelMotion==new Vector3(1,0,1),"siphon never enters anchor teleport path");
                a.Input.Yaw=180;Check(TechniqueRefusal(a,"e",in a.Input)==null&&TryTechnique(a),"siphon recast does not demand a new aimed target");
                Third("darkness","soul-siphon");a.Seat.chips["ab-soul-feast"]=1;a.Stamina=0;TryTechnique(a);Release();Advance(.5f);StepTechniqueWorld();Check(Math.Abs(a.Stamina-5)<.01&&Math.Abs(b.Stamina-95)<.01,"soul feast transfers 10 resource/s");
                Third("fire","dragon-breath");TryTechnique(a);Release();Advance(.1f);StepTechniqueWorld();Check(b.Hp<100&&b.Dots.Any(x=>x.Key.EndsWith(":fire")),"breath cone damage and burn");
                a.Input.Yaw=180;var prior=a.Technique.Dir;StepTechniqueWorld();Check(Vector3.Angle(Vec(prior),Vec(a.Technique.Dir))<=2.01f,"breath turn capped at 120 degrees/s");a.StunEnd=Time+1;StepTechniqueInput(a);Check(!NativeTechniqueRules.Live(a.Technique,Time),"stun cancels breath");
                Third("fire","combust");Check(!TryTechnique(a)&&a.Mana==100,"combust empty refusal costs nothing");RefreshDot(b,a,"fire",10,4);TryTechnique(a);Release();Check(b.Dots.Count==0&&b.Hp==78&&a.Technique.Path.Length==1,"combust consumes burn and caps base hit at 22");
                Third("daggers","envenom");TryTechnique(a);Release();Damage(b,a,1,Vector3.forward,0,false,primary:false);Check(a.Technique.Arm==2&&b.Dots.Any(x=>x.Key.EndsWith(":poison")&&x.Value.Dps==2)&&b.SlowFactor==.85f,"envenom consumes a hit and poisons/slows");
                Setup("pistol");a.Seat.chips["wild-cornered"]=3;a.Hp=30;double ordinary=a.Stats["attackRate"];StepExpansionPilot(a);Check(Math.Abs(a.Stats["attackRate"]-ordinary*1.75)<.001,"cornered additive stacks");a.Hp=100;StepExpansionPilot(a);Check(Math.Abs(a.Stats["attackRate"]-ordinary)<.001,"cornered restores rate");
                a.Seat.chips["wild-frenzy-scent"]=1;double walking=a.Stats["speed"];Damage(b,a,1,Vector3.forward,0,false,primary:false);StepExpansionPilot(a);Check(Math.Abs(a.Stats["speed"]-walking*1.2)<.001,"frenzy speed after hit");Advance(2.01f);StepExpansionPilot(a);Check(Math.Abs(a.Stats["speed"]-walking)<.001,"frenzy expires");
                Setup("pistol");a.Seat.chips["wild-time-thief"]=1;a.QReady=a.EReady=Time+10;b.QReady=b.EReady=Time+5;float thiefAt=Time;Damage(b,a,1,Vector3.forward,0,false,primary:false);Damage(b,a,1,Vector3.forward,0,false,primary:false);
                Check(a.QReady==thiefAt+9.75f&&b.EReady==thiefAt+5.25f&&Events.Count(e=>e.Kind==EventKind.ChipProc&&e.Detail=="wild-time-thief")==1,"time thief rate limit and both deadlines");
                Setup("pistol");a.Seat.chips["wild-hush"]=1;a.BlockProcUsed=false;BlockProc(a,b,a.Body.position);Check(NativeCombatMarks.Has(b.TechniqueMarks,"wild-hush",Time),"hush writes victim mark");
                Setup("pistol");a.Seat.chips["wild-bodyguard-drone"]=1;var intercepted=new NativeBullet{Id=nextBullet++,Owner=b.Seat.id,Original=b.Seat.id,Spec="pistol",Position=a.Body.position+Vector3.forward,Velocity=Vector3.back*20};Bullets.Add(intercepted);StepExpansionPilot(a);Check(!Bullets.Contains(intercepted)&&a.ExpansionDroneAt==Time+3&&Events.Any(e=>e.Kind==EventKind.ChipProc&&e.Projectile==intercepted.Id),"drone intercept records exact projectile");
                Setup("pistol");a.Seat.chips["wild-bodyguard-drone"]=1;pveAcid.Clear();pveAcid.Add(new PveAcidState{Record=new NativeAcidRecord{id=1,actor=10001,at=Time,flight=1,from=Vec(a.Body.position+Vector3.forward),to=Vec(a.Body.position)}});StepExpansionPilot(a);Check(pveAcid.Count==0,"drone intercepts airborne acid without leaving a pool");
                Setup("pistol");a.Seat.chips["wild-doomsday-clock"]=1;a.Hp=50;StepExpansionPilot(a);Advance(3.01f);StepExpansionPilot(a);Check(a.Hp==70&&b.Hp<100&&a.ChipGauges.Single().Value==0,"doomsday stationary burst heal and reset");
                var expansionWire=NativeFrameCodec.Clone(NetworkFrame());Check(expansionWire.pilots.First(x=>x.id==a.Seat.id).expansionDoomAt==a.ExpansionDoomAt,"proc clock Unity serialization");
                // Pending look must win over stale pilot input, without casting or mutating a replica.
                Setup("rifle");wall.SetActive(true);Physics.SyncTransforms();
                var pendingAim=new InputFrame{Yaw=0,Pitch=0};a.Input.Pitch=90;
                Check(TechniqueRefusal(a,in pendingAim)==null,"pending aim finds grapple surface despite stale sky input");
                Check(TechniqueRefusal(a,in a.Input)==NativeTechniques.NoSurface&&!TryTechnique(a),"sky refusal matches cast");
                Check(a.Technique==null&&a.EReady==0&&a.Stamina==100&&a.Mana==100,"refused press costs nothing");
                string unchanged=JsonUtility.ToJson(a.Input);var position=a.Body.position;var previewVelocity=a.Body.linearVelocity;int events=Events.Count;
                bool wasReplica=Replica;try{Replica=true;for(int i=0;i<100;i++)Check(TechniqueRefusal(a,in pendingAim)==null,"replica query accepts pending look");}finally{Replica=wasReplica;}
                Check(JsonUtility.ToJson(a.Input)==unchanged&&a.Body.position==position&&a.Body.linearVelocity==previewVelocity&&Events.Count==events&&a.Technique==null&&a.EReady==0,"query is read-only");
                var oldArea=techniqueReplicaArea;int oldGeneration=expeditionCollisionGeneration;
                try
                {
                    Replica=true;expeditionCollisionGeneration=Generation;techniqueReplicaArea=new ExpeditionAreaLayout{map=MapId,role="world",sanctuary=new[]{new ExpeditionVolume{x=1000,y=100,z=1005,sx=4,sy=8,sz=2}}};
                    Check(TechniqueRefusal(a,in pendingAim)==NativeTechniques.NoSurface,"guest grapple respects loaded sanctuary crossing");
                    techniqueReplicaArea.role="instance";Check(TechniqueRefusal(a,in pendingAim)==null,"instance does not inherit hub refusal");
                }
                finally{Replica=wasReplica;techniqueReplicaArea=oldArea;expeditionCollisionGeneration=oldGeneration;}
                wall.transform.position+=Vector3.forward*11;Physics.SyncTransforms();Check(TechniqueRefusal(a,in pendingAim)==NativeTechniques.NoSurface,"surface beyond 20 m refused");
                wall.transform.position=new Vector3(1000,103,1010);a.Input=pendingAim;Physics.SyncTransforms();Check(TryTechnique(a)&&a.Technique.Point.z<1010,"cast uses the queried surface");
                Setup("water");pendingAim=new InputFrame{Yaw=90,Pitch=-30};a.Input.Yaw=-90;a.Input.Pitch=0;
                Check(TechniqueRefusal(a,in pendingAim,out var spring,"e")==null&&spring.x>a.Body.position.x,"spring follows pending look");
                Check(AimedAbilityPoint(a,14)==AimedAbilityPoint(a,14,in a.Input),"legacy aim wrapper unchanged");
                pendingAim=new InputFrame{Yaw=0,Pitch=0,AbilityTarget=new V3(1000,100.52f,1004.6f)};a.Input=new InputFrame{Yaw=0,Pitch=0};
                Check(AimedAbilityPoint(a,14,in pendingAim).z<1006&&AimedAbilityPoint(a,14).z>1010,"pending AbilityTarget confirms body depth without borrowing stale input");
                floor.SetActive(false);Physics.SyncTransforms();Check(TechniqueRefusal(a,in pendingAim)==NativeTechniques.NoFloor,"missing spring floor");a.Input=pendingAim;
                Check(!TryTechnique(a)&&a.Mana==100&&a.EReady==0&&a.Technique==null,"no-floor press refuses without charge");floor.SetActive(true);Physics.SyncTransforms();
                a.Body.position+=Vector3.up*20;Physics.SyncTransforms();Check(TechniqueRefusal(a,in pendingAim)==NativeTechniques.NoFloor,"spring floor beyond 14.5 m refused");
                Setup("lightning");TryTechnique(a);Release();Advance(.3f);var recall=a.Body.position;a.Body.position+=Vector3.right*5;Physics.SyncTransforms();
                Check(TechniqueRefusal(a,in pendingAim)==null,"clear recall path");
                b.Body.position=recall;Physics.SyncTransforms();Check(TechniqueRefusal(a,in pendingAim)==NativeTechniques.Blocked,"occupied recall destination");b.Body.position=recall+Vector3.forward*5;
                wall.transform.position=recall+Vector3.right*2.5f;wall.transform.localScale=new Vector3(.2f,2,3);wall.SetActive(true);Physics.SyncTransforms();
                float deadline=a.EReady;position=a.Body.position;var before=a.Technique.ViewCopy();
                Check(TechniqueRefusal(a,in pendingAim)==NativeTechniques.Blocked&&!TryTechnique(a),"capsule-obstructed recall refused by HUD and cast");
                Check(a.Body.position==position&&a.EReady==deadline&&a.Technique.RecastAt==before.RecastAt&&a.Technique.Phase==before.Phase,"blocked recast leaves record/deadline untouched");
                a.Technique.Point=new V3(900,99.135f,1000);Check(TechniqueRefusal(a,in pendingAim)==NativeTechniques.Blocked,"out-of-bounds recall destination");
                a.Technique.Until=Time;Check(TechniqueRefusal(a,in pendingAim)==null,"no recall refusal outside recast window");
                wall.transform.position=new Vector3(1000,103,1010);wall.transform.localScale=new Vector3(10,10,.2f);
                Setup("sword");a.StunEnd=Time+1;a.Mana=a.Stamina=0;a.EReady=Time+10;Check(TechniqueRefusal(a,in pendingAim)==null,"nongeometric refusals have no aim reason");
                foreach(var d in NativeTechniques.All)
                {
                    Setup(d.Spec);a.Seat.weaponTechniques=NativeTechniqueLoadout.With("",d.Spec,"e",d.Id);if(d.Spec=="rifle"||d.Id=="tripwire"){wall.SetActive(true);Physics.SyncTransforms();}
                    a.StunEnd=Time+1;Check(!TryTechnique(a),"stun refuses "+d.Id);a.StunEnd=0;
                    a.RollEnd=Time+1;Check(!TryTechnique(a),"roll refuses "+d.Id);a.RollEnd=0;
                    a.Mana=a.Stamina=0;if(d.Cost>0)Check(!TryTechnique(a),"unfunded "+d.Id);a.Mana=a.Stamina=100;
                    Check(TryTechnique(a),"cast "+d.Id);Check(a.Technique.Kind==d.Id&&a.Technique.Valid(),"record "+d.Id);Check(!TryTechnique(a),"duplicate press "+d.Id);
                    Check(Math.Abs(a.EReady-Time-d.Cooldown)<.001,"cooldown "+d.Id);
                    Check(Math.Abs((a.Weapon.@class=="melee"?a.Stamina:a.Mana)-(100-d.Cost))<.001,"resource "+d.Id);Release();
                    Check(a.Technique.Valid(),"released record "+d.Id);
                    var frame=NativeFrameCodec.Clone(NetworkFrame());Check(frame.snapshot.Actors.First(x=>x.Id==a.Seat.id).Technique?.Kind==d.Id,"Unity codec actor "+d.Id);Check(Math.Abs(frame.pilots.First(x=>x.id==a.Seat.id).eReady-a.EReady)<.001,"Unity codec deadline "+d.Id);
                }
                Setup("sword");a.Seat.weaponTechniques="";a.Input.SirenAbility=1;StepTechniqueInput(a);Check(a.Technique==null,"key one and default loadout do not cast techniques");
                a.Seat.weaponTechniques="sword:q";a.Input.AbilityQ=true;StepTechniqueInput(a);Check(a.Technique?.Kind=="riposte"&&!a.Input.AbilityQ&&a.QReady>Time&&a.EReady==0,"Q replacement consumes Q and tracks Q cooldown only");
                Setup("fire");a.EReady=Time+10;a.ECharges=3;a.Input.AbilityE=true;StepTechniqueInput(a);StepAbilities(a);Check(a.Technique==null&&a.AbilityCast==null&&!a.Input.AbilityE&&a.EReady==Time+10,"E cooldown refuses both technique and replaced spell despite charges");
                Setup("sword");a.Seat.bot=true;Check(!TryTechnique(a),"bots retain defaults");
                Setup("rifle");a.Seat.weaponTechniques="rifle:q";Check(!TryTechnique(a),"scope cannot be replaced");
                Setup("sword");TryTechnique(a);Release();float hp=a.Hp;Check(TryTechniqueParry(a,Vector3.back,a.Body.position,b),"frontal parry");Check(a.Hp==hp&&b.Hp==78&&b.StunEnd>Time,"22 counter / .35 stun");Check(!TryTechniqueParry(a,Vector3.back,a.Body.position,b),"only one counter");
                Setup("sword");TryTechnique(a);Release();var incoming=new NativeBullet{Owner=b.Seat.id,Original=b.Seat.id,Spec="pistol",Velocity=Vector3.back*50,Damage=10,Position=a.Body.position};
                Check(!BulletImpact(incoming,b,new NativeContact{Collider=a.Collider,Point=a.Body.position,Contact=a.Body.position,Normal=Vector3.forward})&&incoming.Owner==a.Seat.id&&incoming.Velocity.z>0&&a.Hp==100,"projectile parry follows defender aim");
                Setup("sword");TryTechnique(a);Release();Check(!TryTechniqueParry(a,Vector3.forward,a.Body.position,b),"rear bypass");Advance(.5f);Check(!TryTechniqueParry(a,Vector3.back,a.Body.position,b)&&TechniqueBusy(a),"unused recovery punish");
                Setup("shield");TryTechnique(a);Release();Check(!TryBlock(a),"planted shield cannot block");var foot=Vec(a.Technique.Point);Check(TechniqueObstacle(b,foot+new Vector3(0,1,2),Vector3.back*4,.045f,40,out _),"wall stops incoming shot");Check(Math.Abs(a.Technique.Arm-2f/3)<.001,"wall health share");Check(TechniqueObstacle(a,foot+new Vector3(0,1,-2),Vector3.forward*4,.045f,80,out _)&&a.Technique.Until<=Time,"wall stops friendly shot and breaks");
                Setup("machinegun");TryTechnique(a);Release();Advance(.61f);StepTechniqueWorld();Check(Bullets.Count==1&&Bullets[0].Damage==5&&Events.Any(e=>e.Kind==EventKind.Shot&&e.Detail=="sentry"),"armed sentry shoots");Check(TechniqueObstacle(b,Vec(a.Technique.Point)+new Vector3(0,.65f,2),Vector3.back*4,.045f,40,out _)&&a.TechniqueHealth==0,"sentry destroyed");
                Setup("pistol");TryTechnique(a);Release();var coin=NativeTechniques.CoinAt(a.Technique,Time);var round=new NativeBullet{Id=nextBullet++,Owner=a.Seat.id,Original=a.Seat.id,Spec="pistol",Position=new Vector3(coin.X,coin.Y,coin.Z-1),Velocity=Vector3.forward*60,Damage=10,Radius=.045f};Bullets.Add(round);Check(TechniqueBullet(round,a,Vector3.forward*2)&&round.Damage==16&&round.TechniqueNoHead,"coin turns and amplifies round without headshots");
                Setup("fire");TryTechnique(a);Release();Check(a.Body.linearVelocity.y>17&&!a.Grounded,"ignition launch");
                Setup("water");TryTechnique(a);Release();a.Body.position=Vec(a.Technique.Point)+Vector3.up*.865f;a.Hp=50;for(int i=0;i<20;i++){StepTechniqueWorld();Advance(.25f);}Check(Math.Abs(a.Hp-80)<.01,"spring caps one pool at 30 healing");
                Setup("lightning");TryTechnique(a);Release();Advance(.3f);var anchor=a.Body.position;a.Body.position+=Vector3.right*5;Check(TryTechnique(a)&&Vector3.Distance(a.Body.position,anchor)<.01,"static return");Check(!TryTechnique(a),"recall consumed");
                Setup("greatsword");TryTechnique(a);Release();for(int i=0;i<40&&a.Technique.Phase=="flight";i++){Advance(1f/60);StepTechniqueWorld();}Check(a.Technique.Phase=="stuck"&&b.Hp==82,"anchor sweeps enemy / 18 damage");Advance(.4f);Check(TryTechnique(a)&&a.Technique.Phase=="leap","anchor recast");var velocity=Vector3.zero;TechniqueMotion(a,ref velocity);Check(velocity.magnitude>0,"leap actually moves");
                Setup("rifle");wall.SetActive(true);Physics.SyncTransforms();TryTechnique(a);Release();Advance(.3f);StepTechniqueWorld();velocity=Vector3.zero;TechniqueMotion(a,ref velocity);Check(Math.Abs(velocity.magnitude-16)<.01,"grapple 16 m/s");a.Input.Jump=true;StepTechniqueInput(a);Check(!NativeTechniqueRules.Live(a.Technique,Time),"jump releases grapple");
                Setup("darkness");TryTechnique(a);Release();for(int i=0;i<25&&a.Technique.Phase=="flight";i++){Advance(1f/60);StepTechniqueWorld();}Check(a.Technique.Target==b.Seat.id&&a.Technique.Phase=="swap","shade contact");var start=a.Body.position;var end=b.Body.position;Advance(.251f);StepTechniqueWorld();Check(a.Body.position==end&&b.Body.position==start,"shade exchanges positions");
                Setup("daggers");TryTechnique(a);Release();Check(TechniqueSmokeHidden(a.Body.position+Vector3.right*10,a.Body.position)&&!TechniqueSmokeHidden(a.Body.position,a.Body.position),"smoke outside/inside counter");
                Setup("bow");TryTechnique(a);Release();for(int i=0;i<20&&a.Technique.Phase=="flight";i++){Advance(1f/60);StepTechniqueWorld();}Check(a.Technique.Phase=="burst"&&b.Hp==94&&b.Knockback.magnitude>0,"gale impact damage and shove");
                Setup("fire");TryTechnique(a);a.StunEnd=Time+2;Advance(.2f);StepTechniqueInput(a);Check(!a.TechniqueReleased,"interrupted windup cannot release");
                void More(string spec,string id){Setup(spec);a.Seat.weaponTechniques=NativeTechniqueLoadout.With("",spec,"e",id);}
                More("sword","parting-line");a.Seat.weaponTechniques=NativeTechniqueLoadout.With(a.Seat.weaponTechniques,"sword","q","riposte");
                a.Input.AbilityE=true;StepTechniqueInput(a);Check(a.Technique.Kind=="parting-line"&&a.QReady==0&&a.EReady>Time,"E resolves its own id with two techniques equipped");
                Release();velocity=Vector3.zero;TechniqueMotion(a,ref velocity);Check(velocity.z<-16,"parting hop travels backward");
                var lineOrigin=a.Body.position;for(int hop=0;hop<19;hop++){velocity=Vector3.zero;TechniqueMotion(a,ref velocity);a.Body.position+=velocity/60;Physics.SyncTransforms();Advance(1f/60);}Check(a.Body.position.z<lineOrigin.z-4.3f,"full parting hop clears floor without shortening");var landed=a.Body.position;velocity=Vector3.zero;a.Technique.Phase="line";TechniqueMotion(a,ref velocity);Check(a.Body.position==landed&&velocity==Vector3.zero,"completed hop cannot enter anchor teleport");a.Technique.Phase="line";var hopRelease=a.Technique.Release;a.Technique.Release=Time-.1f;velocity=Vector3.zero;TechniqueMotion(a,ref velocity);Check(a.Body.position==landed&&velocity==Vector3.zero,"early wall end cannot snap onto line");a.Technique.Release=hopRelease;
                b.Body.position=lineOrigin+Vector3.forward*.5f;Physics.SyncTransforms();Advance(.4f);StepTechniqueWorld();Check(b.Hp==86&&b.Knockback.magnitude>0&&a.Technique.Phase=="burst","parting line delayed burst and shove");StepTechniqueWorld();Check(b.Hp==86,"parting burst once");
                More("daggers","tripwire");a.Input.Pitch=90;Check(TechniqueRefusal(a,"e",in a.Input)==NativeTechniques.NoSurface&&!TryTechnique(a),"tripwire refuses sky without cost");a.Input.Pitch=0;wall.SetActive(true);Physics.SyncTransforms();Check(TryTechnique(a),"tripwire surface");Release();Advance(.33f);StepTechniqueWorld();Check(b.Hp==92&&b.SlowFactor==.4f&&a.Technique.Phase=="snap","tripwire first crossing damage/snare");StepTechniqueWorld();Check(b.Hp==92,"wire consumed once");
                More("shield","shield-surf");TryTechnique(a);Release();velocity=Vector3.zero;TechniqueMotion(a,ref velocity);Check(Math.Abs(velocity.z-11)<.01&&!TryBlock(a),"surf speed and no block");a.Input.Yaw=90;TechniqueMotion(a,ref velocity);Check(velocity.x>10.9f,"surf steers");Check(TryTechnique(a)&&a.Technique.Phase=="hop"&&a.Technique.RecastAt==Time,"surf recasts despite own action lock");
                More("greatsword","iron-vow");TryTechnique(a);Release();float dealt=Damage(a,b,20,Vector3.back,100,false);Check(Math.Abs(dealt-12)<.01&&a.Technique.HitAt==Time,"vow absorbs forty percent and records hit");StunMelee(a,1);Check(a.StunEnd<=Time,"vow stagger immunity");velocity=Vector3.one;TechniqueMotion(a,ref velocity);Check(velocity.x==0&&velocity.z==0,"vow rooted");b.Body.position=a.Body.position+Vector3.forward*2;Physics.SyncTransforms();Check(TryTechnique(a)&&b.Knockback.magnitude>0&&a.Technique.Phase=="release","vow early release shoves");
                More("pistol","bounty-round");TryTechnique(a);Release();Advance(.1f);StepTechniqueWorld();Check(a.Technique.Target==b.Seat.id&&b.Hp==94&&a.Technique.Phase=="marked","bounty marks actual hit");Damage(b,a,200,Vector3.forward,0,false);Check(a.EReady==Time,"marked death refunds owning slot");
                More("machinegun","recoil-burst");a.Ammo=0;Check(!TryTechnique(a)&&a.EReady==0,"recoil requires ammo");a.Ammo=1;TryTechnique(a);Release();Check(a.Ammo==0&&b.Hp==90,"recoil consumes magazine and close cone hits");velocity=Vector3.zero;TechniqueMotion(a,ref velocity);Check(velocity.z==-12,"recoil opposite aim");Advance(.46f);StepTechniqueWorld();Check(a.ReloadEnd>Time,"recoil starts reload after travel");
                More("rifle","sonar-dart");a.Input.Pitch=90;Check(TechniqueRefusal(a,"e",in a.Input)==NativeTechniques.NoSurface&&!TryTechnique(a),"sonar sky refusal");a.Input.Pitch=0;wall.SetActive(true);Physics.SyncTransforms();TryTechnique(a);Release();Check(a.TechniqueHealth==10&&Math.Abs(a.Technique.Until-a.Technique.Arm-3.6f)<.001,"sonar three ping windows");DamageTechniqueObject(a,10,Vec(a.Technique.Point));Check(a.Technique.HitAt==Time&&a.Technique.Until==Time,"destroyed sonar immediately stops reveal");
                More("bow","spirit-hawk");TryTechnique(a);Release();for(int n=0;n<80&&a.Technique.HitAt<-50;n++){Advance(1f/60);StepTechniqueWorld();}Check(b.Hp==90&&b.SlowFactor==.8f&&a.Technique.HitAt>-50,"hawk acquires dives hits and slows");
                More("fire","wildfire-run");TryTechnique(a);Release();Check(MoreTechniqueSpeed(a,Vector3.forward)==1.35f,"wildfire speed");for(int n=0;n<24;n++){a.Body.position+=Vector3.right*.1f;Advance(.125f);StepTechniqueWorld();}Check(a.Technique.Path.Length==24&&a.Technique.Valid()&&!TechniqueRunning(a),"bounded timed path and run end");var pathCopy=a.Technique.ViewCopy();a.Technique.Path[0].x+=2;Check(pathCopy.Path[0].x!=a.Technique.Path[0].x,"native trail copy isolation");
                More("water","slipstream");TryTechnique(a);Release();Check(Math.Abs(MoreTechniqueSpeed(b,Vector3.forward)-1.4f)<.001&&Math.Abs(MoreTechniqueSpeed(b,Vector3.back)-.7f)<.001,"current helps enemies along and hinders against");
                More("lightning","lightning-rod");TryTechnique(a);Release();Advance(.31f);StepTechniqueWorld();var rod=Vec(a.Technique.Point)+Vector3.up*.6f;Check(TechniqueObstacle(b,rod+Vector3.left*4,Vector3.right*8,.04f,5,out _)&&a.TechniqueHealth==55&&a.Technique.HitAt==Time,"rod draws nearby enemy shot and records contact");DamageTechniqueObject(a,55,rod);Check(a.Technique.Until<=Time,"rod health exhaustion");
                More("darkness","grave-walk");TryTechnique(a);Release();Check(Damage(a,b,20,Vector3.zero,0,false)==0&&Invulnerable(a),"grave walk untouchable");a.Input.X=1;velocity=Vector3.zero;TechniqueMotion(a,ref velocity);Check(velocity.x==7,"grave walk speed");Check(TryTechnique(a)&&!Invulnerable(a)&&a.Technique.Phase=="rise"&&TechniqueBusy(a),"early rise removes immunity but locks recovery");
                Debug.Log("PASS native weapon technique simulation: "+checks+" checks, including actual Unity codec and isolated collision handlers");
            }
            finally{Map.bounds=oldBounds;CapturedAnimation.Release(floor);CapturedAnimation.Release(wall);a.Technique=b.Technique=null;Bullets.Clear();}
        }
    }
}
#endif
