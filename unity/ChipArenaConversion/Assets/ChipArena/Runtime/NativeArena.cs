using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    // Unity owns authoritative rigid bodies, contact queries and the match clock.
    // Draft support is checked against the pinned stat and mechanic catalogs.
    public sealed class NativePilotBody : MonoBehaviour { public NativePilot Pilot; }
    public sealed class NativePropBody : MonoBehaviour
    {
        public NativeArena Arena;public NativePilot Source;public float SourceAt;public string Group;public bool Fragment;public ContentBlock Definition;public float Health;public bool Indestructible;public Rigidbody Body;
        // Authored PvE machines are built identically on every peer, outside map prop replication.
        public bool LocalFixture;
        public bool Replicated=>!LocalFixture&&Definition.role!="carried";
        private void OnCollisionEnter(Collision c)=>Arena?.PropContact(this,c);private void OnCollisionStay(Collision c)=>Arena?.PropContact(this,c);
    }
    public sealed partial class NativePilot
    {
        public NativeSeat Seat;public ContentWeapon Weapon;public ChipStats Stats;public Rigidbody Body;public CapsuleCollider Collider;
        public NativeMeleeKit MeleeKit=new NativeMeleeKit();public float MeleeBufferedUntil=-1,MeleeComboUntil,ArmourHitUntil;public NativeMeleeAction Special;public NativeRecovery Down;
        public float LastDamage=-100,ShieldUntil,EmpowerUntil,LeechAt=-100,LeechPending;public int DirectHits,VolleyIndex,EmpowerShots;public bool BlockProcUsed;
        public readonly NativeBotBrain BotBrain=new NativeBotBrain();public Vector3 BotPrevious;public float BotBlocked;public int BotSide;public NativeMeleeSwing Swing;public Vector3 MoveVelocity;public float BlockAt=-100,RiposteAt=-100,RiposteUntil;public int RiposteBy=-1;
        public Actor Pose=new Actor();public InputFrame Input;public float Hp,Mana,Stamina,Yaw,Pitch;
        public NativeFanBurst Fan;public float OverdriveAt,OverdriveUntil,VentUntil;
        public int Ammo,Combo=-1,AimShots;public float Ready,RegenAt,ReloadAt,ReloadEnd,BlockEnd,BlockReady,RollAt=-100,RollEnd,RollReady,StunEnd,StaggerImmune,CastAt,SlowEnd,SoakedAt=-100;
        public float AttackAt=-100,HitAt,AttackEnd,LastAttack=-100,LastShot=-100,AimAt=-100,QReady,EReady;
        public float AirborneAt=-1;public bool AirAttackUsed;public int AirJumps;public float AirJumpAt=-100;
        public Vector3 Knockback,RollDirection;public RollValues Roll;public bool HitPending,Aiming,SprintExhausted,Grounded;
        public NativeAbilityCast AbilityCast;public string BufferedSlot;public float BufferedAt,HoldAt=-1,SlowFactor=1,SlowImmune;public int ECharges=1;
        public readonly Dictionary<string,NativeDot> Dots=new Dictionary<string,NativeDot>();
    }
    public sealed class NativeDot { public bool HubNoReward;public int Source;public double Dps,Next,Until; }
    public sealed class NativeBullet
    {
        public float Draw;public string Detail;public int Id,Owner,Reflections;public Vector3 Position,Velocity;public float Damage,Gravity,Radius,Life=3,Travel=160,Impulse;
        public string SirenKind;public int SirenTarget;public string LeetKind;public Vector3? LockTarget;public float ReleaseAt;public bool LeetLaunched;public string Spec;public HashSet<int> Hit=new HashSet<int>();
        public int Original,Attack,Bounces,Pierces;public float Born,Travelled,FixedSpeed,BounceRetention= .7f,Homing,Burn,Poison,Demolition=1,ReturnAt=-1,RallyCap,RallyBonus;public NativeShotScale Motion=new NativeShotScale();public float MotionScale{get=>Motion.Value;set=>Motion.Value=value;}
        public bool CyberPacket;public bool Hadoken,Super;public bool Direct=true,ChipImpact,WeaponFx,EffectsUsed,Pong,Boomerang,Zigzag,Relaunched;public Vector3 RallyMark,BaseVelocity;
        public Dictionary<string,int> Mods=new Dictionary<string,int>();public readonly Dictionary<int,float> Contacts=new Dictionary<int,float>();
    }
    public sealed partial class NativeArena : IDisposable
    {
        public const int ActorLayer=8,PropLayer=9;
        public const int WorldMask=1<<PropLayer,ShotMask=(1<<PropLayer)|(1<<ActorLayer)|(1<<11);
        public readonly ConversionContent Content;public readonly NativeMatch Match;
        public readonly List<NativePilot> Pilots=new List<NativePilot>();public readonly List<NativeBullet> Bullets=new List<NativeBullet>();
        public readonly List<NativePropBody> Props=new List<NativePropBody>();
        public readonly List<NativePropBody> StaticBlocks=new List<NativePropBody>();
        public IEnumerable<NativePropBody> AllBlocks=>Props.Concat(StaticBlocks);
        public ContentMap Map=>Content.Map(MapId);public NativeStructureState Structures{get;private set;}
        public Vector3 ClampMap(Vector3 p,bool bot=false)=>Vec(NativeMapRules.Clamp(Map,Vec(p),bot));
        public readonly NativeEventBuffer Events=new NativeEventBuffer();
        public string MapId{get;private set;}public bool RandomizeMapEachSet;private int mapSet;public bool FriendlyFire,StationaryBots;
        public int Generation {get;private set;}
        private GameObject root;private int nextBullet=1,nextEvent=1,nextAttack=1;private readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private readonly Dictionary<string,PhysicsMaterial> surfaces=new Dictionary<string,PhysicsMaterial>();
        private readonly Dictionary<string,float> hazardHits=new Dictionary<string,float>();
        public GameObject Root=>root;
        public UnityEngine.SceneManagement.Scene ReplicaScene;
        public bool ReplaySampledShots;private readonly NativeShotEnds shotEnds=new NativeShotEnds();
        private readonly System.Random random;private readonly RaycastHit[] rayHits=new RaycastHit[256];
        public float Time=>(float)Match.Time;
        public static readonly HashSet<string> SupportedStats=new HashSet<string>{"rollDistance","rollSpeed","rollCost","rollWindow","reloadSpeed","resourceCost","manaRegen","staminaRegen","projectileGravity","projectileSize","fanDegrees","bounceRetention","lifeSteal","execute","closeDamage","farDamage","backstab","thorns","regeneration","reloadHeal","lastRound","blockHeal","blockAmmo","blockForce","dashBlast","meleeArc","jumpPower","playerGravity","recoil","critical","vacuum","pong","boomerang","zigzag","chaos","hp","speed","attackRate","damage","damageTaken","knockbackTaken","dashCooldown","bounces","split","pierces","burn","poison","staticEvery","staticDamage","reach","impulse","demolition","shrapnel","debrisTaken","extraProjectiles","extraStrikes","ammoCapacity","maxMana","maxStamina","projectileSpeed","homing","blockCooldown","blockWindow","reflectDamage","reflectSpeed","blockPulseDamage","blockPulseImpulse","abilityCooldown","abilityPower","abilityRadius","abilityCost","abilityCharges","qEmpower","manaSurge","abilityLeech","arcaneSurge","spellEcho"};
        public static readonly HashSet<string> SupportedMechanics=new HashSet<string>{"abyssal-core","accelerating-shot","acid-rain","aftershock","arcade-afterimage","arcade-cancel","arcade-counter","arcade-cyclone","arcade-focus","arcade-juggle","arcade-perfect","arcade-red","arcade-rising","arcade-rush","arcade-secondwind","arcade-shinku","artillery","backfire","black-hole","blink-cut","blizzard","bowling-ball","bread-crumbs","bubble-mine","cinder-field","cluster-mine","coin-toss","comet","corkscrew","corpse-party","crossfire","delayed-gratification","demolition-charge","elevator-shot","freezer-block","grappling-shot","graveyard-shift","helix","hot-foot","implosion-bomb","l44t-brain","l44t-copy","l44t-nuke","l44t-scare","l44t-time","l44t-undo","l44t-uprising","l44t-yeet","lightning-rod","mana-leech","med-garden","mirror-mine","mishima-armor","mishima-bloodline","mishima-bound","mishima-counter","mishima-godfist","mishima-magnet","mishima-pressure","mishima-rage","mishima-screw","mishima-trail","mishima-twin","mishima-wall","necronomicon","orbitals","panic-button","pickpocket","poltergeist","proximity-mail","puppet-master","repulsion-pad","revenge-mail","second-chance","sticky-bomb","stop-go","storm-recall","swap-shop","sword-beam","thunderstep","time-warp","trampoline","undertow","uppercut","vampire-dash","wildfire","wormhole"};
        public static bool ImplementedChip(ContentChip c)=>c!=null&&(string.IsNullOrEmpty(c.mechanic)||SupportedMechanics.Contains(c.mechanic))&&c.effects.All(e=>SupportedStats.Contains(e.stat));
        public NativeArena(ConversionContent content,NativeMatch match,string map,int seed=12345)
        {random=new System.Random(seed);Content=content;Match=match;MapId=map;Match.ResetRound=Reset;Match.HoldResolution=SandboxHolds;Match.IsSandbox=()=>Sandbox;Match.RoundDeaths=()=>Pilots.Where(p=>p.Death!=null).Select(p=>new FinalKillCandidate{Victim=p.Seat.id,Killer=p.Death.owner,Seed=p.Death.seed,At=p.Death.at});Match.DraftFilter=ImplementedChip;Physics.gravity=new Vector3(0,-23,0);}
        public static Color ColorOf(int value)=>new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f);
        public static Vector3 Vec(V3 v)=>new Vector3(v.x,v.y,v.z);
        public static V3 Vec(Vector3 v)=>new V3(v.x,v.y,v.z);
        private void DestroyScene()
        {
            if(root!=null){foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;CapturedAnimation.Release(root);}
            ResetHub();expeditionClosures.Clear();expeditionNavClear.Clear();expeditionDeaths.Clear();expeditionBoss=null;hulkChargeIgnored.Clear();pveAcid.Clear();acidProps.Clear();hordeAdds.Clear();hordeEnemies.Clear();hordeTargets.Clear();hordeGrid.Clear();wardBreaches.Clear();wardBreachRevision=0;wardSandboxState=null;ClearCutExitCollision();stageLoads.Clear();frameProps.Clear();Pilots.Clear();Props.Clear();StaticBlocks.Clear();ResetStructureRuntime();Bullets.Clear();shotEnds.Clear();Events.Clear();hazardHits.Clear();AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();abilityEchoes.Clear();MeleeShots.Clear();MeleeImpacts.Clear();meleeFissures.Clear();meleeCarries.Clear();procLedger.Clear();chipCooldowns.Clear();ChipFields.Clear();Minions.Clear();FighterEchoes.Clear();StormTrails.Clear();LeetFields.Clear();originalInputs.Clear();SirenFields.Clear();sirenFieldAt=0;
        }
        public void Reset()
        {
            if(!Replica&&RandomizeMapEachSet&&Match.Set!=mapSet){var maps=Content.maps.Where(m=>NativeMapRules.ArenaSelectable(m)&&m.rotation&&m.id!=MapId).ToArray();if(mapSet>0&&maps.Length>0)MapId=maps[random.Next(maps.Length)].id;mapSet=Match.Set;}
            DevHazardPulses.Clear();Generation++;Entries.Clear();MinionDeaths.Clear();DestroyScene();Pve=null;pveEntries.Clear();pveNavigation=null;remoteInputs.Clear();debrisLedger.Clear();contacts.Clear();root=new GameObject("Native match authority");
            if(ReplicaScene.IsValid())UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,ReplicaScene);
            var map=Content.Map(MapId);var shader=Resources.Load<Shader>("NativeLit");
            foreach(var definition in Content.materials)if(!materials.ContainsKey(definition.id))
            {materials[definition.id]=new Material(shader){color=ColorOf(definition.id=="foundation"?map.floor:definition.color)};surfaces[definition.id]=new PhysicsMaterial(definition.id){dynamicFriction=definition.friction,staticFriction=definition.friction,bounciness=0};}
            if(!surfaces.ContainsKey("pilot"))surfaces["pilot"]=new PhysicsMaterial("Pilot friction"){dynamicFriction=0,staticFriction=0,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum};
            Structures=new NativeStructureState(map);foreach(var block in map.blocks)AddProp(block);
            BuildCutExitCollision();NameWardPanels();PrepareNavigation();
            for(int i=0;i<Match.Seats.Count;i++)SpawnPilot(Match.Seats[i],i);
            Physics.SyncTransforms();
        }
        private NativePilot SpawnPilot(NativeSeat seat,int index)
        {
                var stats=NativeContentRules.Stats(Content,seat.spec,seat.chips);var weapon=Content.Weapon(seat.spec);
                var obj=new GameObject(seat.name);obj.layer=ActorLayer;obj.transform.SetParent(root.transform);var spawn=NativeMapRules.Spawn(Content,Map,Match.Seats,index,Match.Teams);obj.transform.position=Vec(spawn.pos);
                var capsule=obj.AddComponent<CapsuleCollider>();capsule.radius=NativeLocomotionRules.Radius;capsule.height=2*NativeLocomotionRules.HalfHeight;capsule.sharedMaterial=surfaces["pilot"];
                var body=obj.AddComponent<Rigidbody>();body.mass=65;body.constraints=RigidbodyConstraints.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;
                var p=new NativePilot{Seat=seat,Weapon=weapon,Stats=stats,Body=body,Collider=capsule,Hp=(float)stats["hp"],Mana=(float)stats["maxMana"],Stamina=(float)stats["maxStamina"],Ammo=(int)Math.Ceiling(weapon.ammo*stats["ammoCapacity"]),Yaw=spawn.yaw};
                p.Pose.Id=seat.id;p.Pose.Bot=seat.bot;p.Pose.Weapon=seat.spec=="pistol"?WeaponId.Pistol:seat.spec=="rifle"?WeaponId.Rifle:seat.spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
                p.Pose.VisualSet=seat.spec=="daggers"?1:seat.spec=="greatsword"?2:seat.spec=="shield"?3:0;
                p.Pose.MagicVisual=seat.spec=="fire"?1:seat.spec=="water"?2:seat.spec=="lightning"?3:seat.spec=="darkness"?4:0;
                p.ECharges=NativeAbilityRules.Charges(stats);obj.AddComponent<NativePilotBody>().Pilot=p;Pilots.Add(p);if(!seat.connected){p.Hp=0;seat.alive=false;capsule.enabled=false;body.isKinematic=true;}
            return p;
        }
        public NativePilot Local=>Pilots.Find(p=>p.Seat.id==LocalId);
        public void Step(InputFrame input)
        {
            if(Match.Phase!=NativePhase.Fight)DevClearMonsters();if(BossVictoryTick(input)){DevClearMonsters();return;}CancelInvalidKernel();if(PveHoldTick(1f/60))return;foreach(var p in Pilots)if(!p.Seat.connected&&p.Seat.disconnectedAt>=0&&Time-p.Seat.disconnectedAt>=12){p.Hp=0;p.Seat.alive=false;}DevStep();SandboxStep();Match.Tick(1d/60);if(Pve==null&&Match.Phase==NativePhase.Intro&&Match.IntroStyle=="dev-siren"&&BossSongAt<0)BossSongAt=(float)Match.IntroStarted;if(Pve!=null&&!Pve.Started&&Match.Phase==NativePhase.Fight)StartPve();StepHazards();StepAuthoredHazards();StepSirenStage();StepStructures();StepKernelWorld();if(Match.ResultMovement){StepResultMovement(input);StepResultProjectiles();return;}if(Match.Phase!=NativePhase.Fight){foreach(var p in Pilots)if(p.Body!=null&&!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;return;}
            Events.RemoveAll(e=>Time-e.Time>1); // Preserve the full burst for its natural glow lifetime.
            // Establish every defense before resolving attacks.
            foreach(var p in Pilots)
            {
                if(p.Hp<=0||!p.Seat.connected)continue;p.Input=p.Seat.bot?AvoidVents(p,DevDummyInput(p,SandboxInput(p,DevBot(p)))):p.Seat.id==LocalId?input:RemoteInput(p.Seat.id);p.Input=p.Input.Sanitized();PveInput(p);HubInput(p);
                ProbePilotGround(p);
            }
            RouteLeetInputs();
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&p.Input.Roll)TryRoll(p);
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&p.Input.Block)TryBlock(p);
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected){AdvanceDots(p);if(p.Hp>0&&!Frozen(p))StepPilot(p);}
            StepMeleeKitWorld();StepCyberWorld();StepBullets();StepAbilityWorld();StepMeleeShots();StepChipFields();StepMinions();StepDevMonsters();StepFighterWorld();StepLeetWorld();StepSirenWorld();RestoreLeetInputs();
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&p.Body.position.y<(Map.bounds?.killY??-6)){if(Pve!=null&&p==Pve.Boss)PlacePveBoss(BossSpawn);else Damage(p,null,p.Hp+1,Vector3.zero,0,false,true);}
            float overtime=NativeCombatRules.Overtime(Match.Time-Match.FightAt,Content.config.FirstOrDefault(v=>v.name=="maxRoundSeconds")?.value??100,Match.Cooperative,SandboxAllowed);if(overtime>0)foreach(var p in Pilots)if(p.Hp>0)Damage(p,null,overtime,Vector3.zero,0,false,forced:true);
            StepPve();Match.Resolve();if(Match.Phase!=NativePhase.Fight)DevClearMonsters();CancelInvalidKernel();
        }
        // Result movement has no attack, ability, DOT or bot-combat updates: the winner is already fixed.
        private void StepResultMovement(InputFrame localInput)
        {
            const float dt=1f/60;
            foreach(var p in Pilots)
            {
                if(p.Hp<=0||!p.Seat.connected||p.Body==null||p.Body.isKinematic)continue;
                var raw=(p.Seat.bot?new InputFrame{Yaw=p.Yaw,Pitch=p.Pitch}:p.Seat.id==LocalId?localInput:RemoteInput(p.Seat.id)).Sanitized();
                p.Input=new InputFrame{X=raw.X,Z=raw.Z,Yaw=raw.Yaw,Pitch=raw.Pitch,Sprint=raw.Sprint,Jump=raw.Jump};
                p.Yaw=raw.Yaw;p.Pitch=raw.Pitch;ProbePilotGround(p);
                var move=new Vector3(raw.X,0,raw.Z);bool sprint=raw.Sprint&&p.Stamina>0&&move.magnitude>.5f&&Vector3.Dot(move.normalized,Quaternion.Euler(0,p.Yaw,0)*Vector3.forward)>=.6f;
                if(sprint)p.Stamina=Math.Max(0,p.Stamina-12*dt);
                float speed=(float)(6.8*p.Stats["speed"])*(sprint?1.35f:1);
                p.MoveVelocity=new Vector3(Mathf.MoveTowards(p.MoveVelocity.x,move.x*speed,35*dt),0,Mathf.MoveTowards(p.MoveVelocity.z,move.z*speed,35*dt));
                var velocity=new Vector3(p.MoveVelocity.x,p.Body.linearVelocity.y,p.MoveVelocity.z);
                var jump=NativeJumpSimulation.Try(p.Seat.spec,raw.Jump,p.Grounded,false,false,velocity.y,(float)(Rules.JumpSpeed*p.Stats["jumpPower"]),ref p.AirJumps,out float launch);
                bool jumped=jump!=NativeJumpKind.None;if(jumped){velocity.y=launch;p.Grounded=false;if(jump==NativeJumpKind.Air)p.AirJumpAt=Time;}
                velocity.y-=(float)(23*(p.Stats["playerGravity"]-1)*dt);p.Body.useGravity=!GroundMotion(p,ref velocity,jumped,false,0);
                p.Body.linearVelocity=velocity;p.Pose.Sprinting=sprint;
            }
        }
        private void StepPilot(NativePilot p)
        {
            p.Body.position=Vec(NativeMapRules.ExcludeTops(Map,Vec(p.Body.position)));
            if(KernelBusy(p))return;if(p.Input.SirenAbility==4&&StartKernelPanic(p))return;
            if(PinSirenVictim(p))return;StepRecovery(p);
            if(Time>=p.SlowEnd&&p.SlowFactor<1){if(p.SlowFactor<=.6f)p.SlowImmune=Time+1;p.SlowFactor=1;}
            var i=p.Input;float dt=1f/60;bool rolling=Time<p.RollEnd,locked=Time<p.AttackEnd&&!(p.Seat.spec=="cyberlord"&&p.Cyber.Until>Time&&NativeCyberKit2Rules.Move(p.Cyber,Time)>0)||Incapacitated(p),swimming=NativeHazards.InLava(MapId,Vec(p.Body.position));
            if(!rolling&&!locked)p.Yaw=i.Yaw;p.Pitch=i.Pitch;
            StepAbilities(p);StepFighter(p);StepLeetPilot(p);StepSirenPilot(p);
            if(p.ReloadEnd>0&&Time>=p.ReloadEnd){p.Ammo=Magazine(p);p.ReloadEnd=0;Heal(p,(float)p.Stats["reloadHeal"]);Emit(EventKind.ReloadEnd,p,0,p.Body.position,0);}
            bool aim=NativeRangedRules.Scope(p.Seat.spec)&&i.Aim&&!rolling&&p.ReloadEnd==0&&Time>=p.BlockEnd&&!Incapacitated(p);
            if(aim&&!p.Aiming)p.AimAt=Time;p.Aiming=aim;
            if(i.Reload&&p.AbilityCast==null)Reload(p);
            if(p.CastAt>0&&Time>=p.CastAt){p.CastAt=0;Fire(p);}
            StepFan(p);StepSpecial(p);StepMelee(p);StepBow(p);
            StepMeleeInput(p);
            locked=Time<p.AttackEnd&&!(p.Seat.spec=="cyberlord"&&p.Cyber.Until>Time&&NativeCyberKit2Rules.Move(p.Cyber,Time)>0)||Incapacitated(p);rolling=Time<p.RollEnd;
            bool whirling=!Incapacitated(p)&&NativeWhirlwindRules.Active(p.Special,Time);
            if(p.Seat.spec!="bow"&&p.Weapon.@class!="melee"&&p.Fan==null&&!Venting(p)&&!Incapacitated(p)&&p.AbilityCast==null&&p.CastAt<=0&&!rolling&&!locked&&Time>=p.Ready&&p.ReloadEnd==0&&Time>=p.BlockEnd&&i.Shoot)
            {
                if(p.Weapon.@class=="ranged"&&p.Ammo<=0)Reload(p);
                else if(p.Weapon.@class=="ranged"||p.Mana>=p.Weapon.cost*p.Stats["resourceCost"])
                {
                    if(p.Weapon.@class=="ranged"){if(!FireRound(p))p.Ready=Time+NativeRangedRules.Interval(p.Weapon.interval,p.Stats["attackRate"],Overdriving(p));}
                    else {p.AttackAt=p.LastAttack=Time;p.Mana-=(float)(p.Weapon.cost*p.Stats["resourceCost"]);p.CastAt=Time+(float)((p.Seat.spec=="fire"?.16:p.Seat.spec=="water"?.12:p.Seat.spec=="lightning"?.065:p.Seat.spec=="leet"?.18:.22)/p.Stats["attackRate"]);p.Ready=Time+(float)(p.Weapon.interval/p.Stats["attackRate"]);}
                }
            }
            CyberStepMotion(p);
            var move=new Vector3(i.X,0,i.Z);if(!i.Sprint)p.SprintExhausted=false;
            bool sprint=i.Sprint&&p.AbilityCast==null&&p.CastAt<=0&&!p.SprintExhausted&&!locked&&!rolling&&!i.Shoot&&!aim&&p.ReloadEnd==0&&Time>=p.BlockEnd&&p.Stamina>0&&move.magnitude>.5f&&Vector3.Dot(move.normalized,Quaternion.Euler(0,p.Yaw,0)*Vector3.forward)>=.6f;
            if(sprint){p.Stamina=Math.Max(0,p.Stamina-12*dt);p.RegenAt=Time+.45f;if(p.Stamina<=0)p.SprintExhausted=true;}
            if(Time>=p.RegenAt&&!sprint)p.Stamina=Math.Min((float)p.Stats["maxStamina"],p.Stamina+(float)(24*p.Stats["staminaRegen"]*dt));
            p.Mana=Math.Min((float)p.Stats["maxMana"],p.Mana+(float)(12*p.Stats["manaRegen"]*dt));
            if(Time-p.LastDamage>=3)Heal(p,(float)(p.Stats["regeneration"]*dt));
            float speed=(float)(6.8*p.Stats["speed"])*(sprint?1.35f:1)*(aim?(float)Content.Ads(p.Seat.spec).move:1)*Math.Max(.4f,Math.Min(Time<p.SlowEnd?p.SlowFactor:1,ChipFieldSpeed(p)))*(swimming?.6f:1);
            if(p.Seat.spec=="cyberlord")speed*=NativeCyberKit2Rules.Move(p.Cyber,Time)*(NativeCyberKit2Rules.Overclock(p.Cyber,Time)?1+CyberKit2Spec.OverclockMove:1);
            if(p.BowDrawAt>=0)speed*=NativeBowRules.Move;
            if(whirling)speed*=NativeWhirlwindRules.MoveScale;
            if(Overdriving(p))speed*=NativeRangedRules.OverdriveMove;
            Vector3 velocity=p.Body.linearVelocity;
            if(rolling){float duration=p.RollEnd-p.RollAt,u=(Time-p.RollAt)/duration,q=(Time+dt-p.RollAt)/duration;var roll=p.RollDirection*(Dev(p,"ansatsuken")?22:(float)p.Roll.distance*(MathUtil.Smooth(q)-MathUtil.Smooth(u))/dt);velocity.x=roll.x;velocity.z=roll.z;}
            else
            {
                // A landing attack carries its committed horizontal velocity (x .6) through the hang and plunge (the guest's seed).
                if(NativeAirAttackSimulation.Falling(p.Swing)&&!Incapacitated(p))p.MoveVelocity=Vec(p.Swing.Carry);
                else if(locked&&!whirling)p.MoveVelocity=Vector3.zero;
                else p.MoveVelocity=new Vector3(Mathf.MoveTowards(p.MoveVelocity.x,move.x*speed,35*dt),0,Mathf.MoveTowards(p.MoveVelocity.z,move.z*speed,35*dt));
                Vector3 step=(p.Grounded||NativeSprintAttack.Hop(p.Swing))&&!Incapacitated(p)?Vec(NativeMeleeRules.Step(p.Swing,Time,dt)):Vector3.zero;
                step+=MeleeAbilityMotion(p)+FighterMotion(p)+SirenMotion(p);velocity.x=p.MoveVelocity.x+step.x;velocity.z=p.MoveVelocity.z+step.z;
            }
            if(swimming&&velocity.y<=2)velocity.y+=Mathf.Clamp((-.95f-p.Body.position.y)*12-velocity.y*4,-35,35)*dt;
            var jump=NativeJumpSimulation.Try(p.Seat.spec,i.Jump,p.Grounded,swimming,rolling||locked,velocity.y,
                (float)(Rules.JumpSpeed*p.Stats["jumpPower"]),ref p.AirJumps,out float launch);
            bool jumped=jump!=NativeJumpKind.None;
            if(jumped){velocity.y=launch;if(p.Grounded)ChipJump(p);p.Grounded=false;if(jump==NativeJumpKind.Air)p.AirJumpAt=Time;}
            p.Body.useGravity=!swimming||velocity.y>2;
            if(p.Body.useGravity)velocity.y-=(float)(23*(p.Stats["playerGravity"]-1)*dt);
            var remaining=Vec(p.Knockback);Vector3 external=Vec(NativeMeleeRules.External(ref remaining,dt,out float steeringSpeed,Time<(p.Fighter?.WaveUntil??0)&&!rolling));if(Time<p.LaunchUntil){external=p.Knockback;external.x*=Mathf.Exp(-3.1f*dt);external.z*=Mathf.Exp(-3.1f*dt);remaining=new V3(external.x,0,external.z);}p.Knockback=Vec(remaining);
            if(!rolling&&(!locked||whirling))
            {
                var controlled=Vec(NativeMeleeRules.CounterSteer(Vec(p.MoveVelocity),Vec(external),steeringSpeed));
                velocity.x=controlled.x;velocity.z=controlled.z;
            }
            var recovery=NativeSpecialRules.RecoveryMotion(p.Down,Time,new V3(i.X,0,i.Z),i.Yaw);
            if(recovery.Length>0){p.MoveVelocity=p.Knockback=Vector3.zero;velocity.x=recovery.x;velocity.z=recovery.z;external=Vector3.zero;}
            velocity+=external;
            if(NativeAirAttackSimulation.Falling(p.Swing)&&(!NativeAirAttackSimulation.Down(p.Swing)||Time>=p.Swing.PlungeAt)){velocity.y=NativeAirAttackSimulation.Vertical(p.Swing,Time);p.Body.useGravity=false;}
            if(GroundMotion(p,ref velocity,jumped,swimming,external.y))p.Body.useGravity=false;
            p.Body.linearVelocity=velocity;

            p.Pose.Sprinting=sprint;
        }
        private int Magazine(NativePilot p)=>(int)Math.Ceiling(p.Weapon.ammo*p.Stats["ammoCapacity"]);
        private void Reload(NativePilot p)
        {if(p.Seat.spec=="bow"||p.Weapon.@class!="ranged"||p.ReloadEnd>0||p.Ammo>=Magazine(p)||Time<p.RollEnd||Incapacitated(p))return;p.Fan=null;p.ReloadAt=Time;p.ReloadEnd=Time+Math.Max(.2f,(float)(p.Weapon.reload*p.Stats["reloadSpeed"]));p.Aiming=false;Emit(EventKind.ReloadStart,p,0,p.Body.position,0);}
        private bool Enemy(NativePilot a,NativePilot b)=>!KernelLocked(b)&&NativeCombatRules.Enemy(a?.Seat,b?.Seat,Match.Teams);
        private bool CanHit(NativePilot a,NativePilot b)=>!KernelLocked(b)&&NativeCombatRules.CanHit(a?.Seat,b?.Seat,Match.Teams,FriendlyFire);
        private bool CanHit(NativePilot source,NativeMinion target)=>target!=null&&!DropHeld(target)&&(target.DevMonster?source?.Seat.connected==true:NativeCombatRules.MinionCanHit(source?.Seat,target.Owner?.Seat,target.Pve!=null,Match.Teams,FriendlyFire));
        private bool Enemy(NativePilot source,NativeMinion target)=>target!=null&&!DropHeld(target)&&(target.DevMonster?source?.Seat.connected==true:NativeCombatRules.MinionEnemy(source?.Seat,target.Owner?.Seat,target.Pve!=null,Match.Teams));
        private bool Invulnerable(NativePilot p)=>p.Roll!=null&&Time>=p.RollAt+p.Roll.iframeStart&&Time<p.RollAt+p.Roll.iframeStart+p.Roll.iframeSeconds;
        private float Damage(NativePilot target,NativePilot source,float amount,Vector3 direction,float impulse,bool head,bool forced=false,bool direct=true,bool primary=true,bool melee=false,bool stage=false,NativeBodyImpact impact=null,Vector3? hitPoint=null,bool rollAvoidable=true,bool weaponHit=false,EventKind eventKind=EventKind.Hit,string damageKind=null,bool sirenCrush=false,string collapseSegment=null,float finalCap=float.PositiveInfinity,int kernelStep=0,string stageHazard=null,bool stageLethal=false,int projectile=0,bool pierced=false)
        {
            if(target.DevGod||BossVictoryWalk||Pve?.Stage=="ended")return 0;
            if(!forced&&HubDamageBlocked(source,target.Body.position,projectile:projectile))return 0;
            if(!NativeKernelPanicRules.AllowsDamage(target.Kernel,target.Seat.id,source?.Kernel,source?.Seat.id??0,kernelStep))return 0;
            if(target.Hp<=0||!target.Seat.connected||source!=null&&!forced&&!(collapseSegment!=null&&source==target)&&!(kernelStep>0?NativeCombatRules.CanHit(source.Seat,target.Seat,Match.Teams,FriendlyFire):CanHit(source,target))||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||!forced&&(target.ScareBy!=null||kernelStep==0&&(Time<target.ShieldUntil||!stage&&NativeSpecialRules.Protected(target.Down,Time)||rollAvoidable&&Invulnerable(target))))return 0;
            if(source!=null&&!forced&&direct&&(primary||melee)&&Time<target.BlockEnd){BlockProc(target,source,hitPoint??target.Collider.ClosestPoint(target.Body.position-direction.normalized*2),"clash");if(melee)SuccessfulMeleeBlock(target,source,amount,direction,impulse);return 0;}
            Vector3? hitFrom=melee&&source!=null?(Vector3?)source.Body.position:null;
            bool shadowHit=source?.Seat.spec=="daggers"&&melee&&weaponHit&&primary&&direct&&!forced&&Time<source.MeleeKit.EmpowerUntil;if(shadowHit)amount*=1.4f;
            if(source!=null&&primary&&direct&&!forced){float chaosRoll=(float)random.NextDouble(),criticalRoll=(float)random.NextDouble();if(criticalRoll<source.Stats["critical"])Emit(EventKind.Critical,source,target.Seat.id,target.Body.position,amount);amount=NativeProjectileRules.Damage(amount,source.Stats,target.Hp,(float)target.Stats["hp"],Vector3.Distance(source.Body.position,target.Body.position),source.Weapon.@class=="melee",Vector3.Dot(Quaternion.Euler(0,target.Input.Yaw,0)*Vector3.forward,(source.Body.position-target.Body.position).normalized),chaosRoll,criticalRoll);}
            amount=forced?amount:amount*(float)target.Stats["damageTaken"]*(Dev(target,"ansatsuken")&&target.Fighter?.Kind=="charge"&&Has(target,"arcade-focus")?.5f:1)*(Dev(target,"emperor")&&target.Fighter?.Kind=="wave"&&Has(target,"mishima-armor")?.5f:1);amount=Math.Min(finalCap,amount);if(!forced&&(kernelStep==0||kernelStep==5)&&Dev(target,"ansatsuken")&&Has(target,"arcade-secondwind")&&amount>=target.Hp&&ProcOnce(target.Seat.id+":round-two")){target.Hp=1;target.Stamina=(float)target.Stats["maxStamina"];target.Dots.Clear();return 0;}amount=stageLethal?target.Hp:Math.Min(finalCap,PveDamageRoom(target,amount,source));if(kernelStep>0&&target.Kernel?.Active==true)amount=NativeKernelPanicRules.HoldLethal(target.Kernel,target.Hp,amount,kernelStep);if(amount<=0)return 0;if(!forced&&LeetSave(target,amount))return 0;if(!stageLethal&&ChipSave(target,amount,forced))return 0;if(SandboxAllowed&&SafeMode&&!target.Seat.bot&&!forced)amount=Math.Min(amount,Math.Max(0,target.Hp-1));float actual=Math.Min(amount,target.Hp);target.Hp=Math.Max(0,target.Hp-amount);target.Pose.FlashUntil=Time+.14f;if(actual>0){target.LastDamage=Time;if(source!=null&&source!=target){lastPilotDamage[target.Seat.id]=(source.Seat.id,Time);target.LastAttacker=source;target.LastAttackerAt=Time;}}
            if(shadowHit&&actual>0)source.MeleeKit.EmpowerUntil=0;
            target.ArmourHitUntil=NativeMeleeKitRules.LightArmour(target.MeleeKit,Time,actual)?Time+.001f:0;
            Wound(target,source,amount,target.Hp<=0,head,melee,direct,direction,impact,hitPoint);MeleeHitReaction(target,direction,hitPoint,actual,head,direct||melee);
            if(source!=null&&primary&&direct&&Leet(source,"yeet")){impulse*=1000;target.LaunchUntil=Time+1;}
            if(!SirenArmor(target))target.Knockback+=direction.normalized*((source!=null&&primary&&direct&&Has(source,"grappling-shot")?0:impulse)/65f)*(float)target.Stats["knockbackTaken"];
            if(source!=null&&primary&&direct)SettleRiposte(target,source,actual);
            if(rollAvoidable&&actual>0&&Time<target.RollEnd){target.Roll=null;target.RollEnd=Time;}
            if(source!=null&&source.Hp>0&&direct&&primary&&actual>0)
            {
                Heal(source,actual*(float)(source.Stats["lifeSteal"]+source.Weapon.drain));source.DirectHits++;
                if(source.Stats["staticEvery"]>0&&source.DirectHits%(int)source.Stats["staticEvery"]==0)ChipBlast(source,target.Body.position,3,(float)source.Stats["staticDamage"],60,false,effect:"static");
                RefreshDot(target,source,"fire",source.Weapon.burn+source.Stats["burn"],2);RefreshDot(target,source,"poison",source.Stats["poison"],4);
                if(target.Stats["thorns"]>0)Damage(source,target,actual*(float)target.Stats["thorns"],Vector3.zero,0,false,false,false,damageKind:"thorns");
                if(source.Seat.spec=="water"&&Time>=target.SoakedAt+1.5f){target.SoakedAt=Time;Slow(target,.8f,.8f);}
                if(source.Weapon.chain>0)
                {var next=Pilots.Where(p=>p!=target&&p.Hp>0&&Enemy(source,p)&&(p.Body.position-target.Body.position).magnitude<4&&!SceneLinecast(target.Body.position,p.Body.position,WorldMask)).OrderBy(p=>(p.Body.position-target.Body.position).sqrMagnitude).FirstOrDefault();if(next!=null)Damage(next,source,(float)source.Weapon.chain,Vector3.zero,0,false,false,false,damageKind:"chain");}
            }
            if(actual>0){PveDamaged(target,source,actual);if(!forced&&(kernelStep==0||kernelStep==5))ChipDamaged(target,source,weaponHit);}
            Emit(eventKind==EventKind.Hit&&head?EventKind.Headshot:eventKind,source,target.Seat.id,hitPoint??(target.Body.position+Vector3.up*.8f),actual,stageHazard??collapseSegment??damageKind??(direct?"direct":"secondary"),from:hitFrom,normal:direction.sqrMagnitude>1e-12f?direction.normalized:Vector3.zero,damageKind:collapseSegment!=null?"collapse":stageHazard!=null||damageKind=="dev-monster"?damageKind:null,projectile:projectile,pierced:pierced);
            if(target.Hp<=0&&collapseSegment==null&&(stage||source==null))source=StageCredit(target);
            if(stage&&actual>0)Emit(EventKind.HazardHit,source,target.Seat.id,target.Body.position,actual,damageKind!=null&&damageKind.StartsWith("stage-",StringComparison.Ordinal)?damageKind.Substring(6):stageHazard!=null?(damageKind=="hazard"?(stageHazard.Contains("saw")?"saw":stageHazard.Contains("crane")?"crane":stageHazard.Contains("press")?"press":"vent"):stageHazard):damageKind??"stage");
            if(target.Hp<=0){target.BowRainAt=-100;target.BowDrawAt=-1;RecordDeath(target,source,direction,impulse,melee,sirenCrush,collapseSegment!=null?"collapse":stageHazard!=null||damageKind=="dev-monster"?damageKind:null,stage,stageHazard);target.Down=null;target.Juggle=null;CancelSiren(target);CancelFighter(target);CancelSpecial(target);CancelMelee(target);CancelAbilities(target);target.OverdriveAt=target.OverdriveUntil=target.VentUntil=0;PveDied(target);target.Seat.alive=false;target.Collider.enabled=false;target.Body.isKinematic=true;Emit(EventKind.Death,source,target.Seat.id,target.Body.position,0,stageHazard??collapseSegment??(damageKind=="dev-monster"?"dev-monster":null),damageKind:collapseSegment!=null?"collapse":stageHazard!=null||damageKind=="dev-monster"?damageKind:null);}
            return actual;
        }
        private void RefreshDot(NativePilot target,NativePilot source,string kind,double dps,double duration)
        {
            if(dps<=0)return;string key=source.Seat.id+":"+kind;target.Dots.TryGetValue(key,out var old);
            target.Dots[key]=new NativeDot{Source=source.Seat.id,Dps=Math.Max(dps,old!=null&&old.Until>=Match.Time?old.Dps:0),Next=old!=null&&old.Until>=Match.Time?old.Next:Match.Time+.25,Until=Match.Time+duration};
        }
        private void AdvanceDots(NativePilot p)
        {foreach(var pair in p.Dots.ToArray()){var dot=pair.Value;while(dot.Next<=Match.Time+1e-8&&dot.Next<=dot.Until+1e-8){Damage(p,Pilots.Find(x=>x.Seat.id==dot.Source),(float)(dot.Dps*.25),Vector3.zero,0,false,false,false,rollAvoidable:false,eventKind:pair.Key.EndsWith(":fire")?EventKind.BurnTick:EventKind.PoisonTick,damageKind:pair.Key=="dev-monster:poison"?"dev-monster":null);dot.Next+=.25;}if(Match.Time>dot.Until+1e-8)p.Dots.Remove(pair.Key);}}
        private void Blast(NativePilot source,Vector3 position,float radius,float damage,float impulse)
        {
            foreach(var p in Pilots){Vector3 delta=p.Body.position-position;if(p.Hp<=0||!CanHit(source,p)||delta.magnitude>radius||SceneLinecast(position+Vector3.up*.05f,p.Body.position,WorldMask))continue;float scale=1-delta.magnitude/radius;Damage(p,source,damage*scale,delta.normalized,impulse*scale,false,false,false,damageKind:"blast");}
            foreach(var prop in Props.ToArray())if(prop!=null&&(prop.transform.position-position).magnitude<radius)DamageProp(prop,damage,(prop.transform.position-position).normalized*impulse,position,source);
        }
        private void StepHazards()
        {
            foreach(var h in NativeHazards.At(MapId,Match.Time))
            {
                if(!h.active||Match.Phase!=NativePhase.Fight||Match.Time-Match.FightAt<2)continue;
                foreach(var p in Pilots)
                {
                    if(p.Hp<=0)continue;var pos=p.Body.position;string key=h.id+":"+p.Seat.id;
                    if(hazardHits.TryGetValue(key,out float until)&&Time<until)continue;
                    bool lava=h.kind=="lava",touch=lava?NativeHazards.InLavaChannel(h,Vec(pos)):h.kind=="saw"?Math.Abs(pos.z-h.z)<.62&&Math.Sqrt((pos.x-h.x)*(pos.x-h.x)+Math.Pow(Math.Max(0,Math.Abs(pos.y-h.y)-.43),2))<1.62:Math.Abs(pos.x-h.x)<h.sx/2+.3&&Math.Abs(pos.z-h.z)<h.sz/2+.3&&pos.y-.85<1.1*h.extension&&pos.y>-.5;
                    if(!touch)continue;hazardHits[key]=Time+(lava?.25f:.65f);Damage(p,null,lava?8:h.kind=="saw"?22:25,lava?Vector3.zero:new Vector3(Mathf.Sign(pos.x-h.x)*.4f,.35f,Mathf.Sign(pos.z-h.z)),lava?0:150,false,direct:false,stage:true,damageKind:"stage-"+h.kind);
                }
            }
        }
        private void DamageProp(NativePropBody prop,float amount,Vector3 impulse,Vector3 point,NativePilot source=null,string kind="other",float demolition=1)
        {
            if(prop==null||prop.Indestructible||!Props.Contains(prop)||!FilterStructureDamage(prop,ref amount,source,kind,demolition))return;bool dynamic=prop.Body!=null&&!prop.Body.isKinematic;
            if(dynamic){prop.Source=source;prop.SourceAt=Time;prop.Body.AddForceAtPosition(impulse,point,ForceMode.Impulse);}prop.Health-=amount;if(prop.Health<=0&&!prop.Fragment)Fracture(prop,dynamic?Vector3.zero:impulse,source);
        }
        private void SurfaceImpact(RaycastHit hit,NativePilot source,int projectile,int color)
        {
            var prop=hit.collider.GetComponent<NativePropBody>();if(prop==null)return;
            Events.Add(new ArenaEvent{Id=nextEvent++,Kind=EventKind.Impact,Source=source?.Seat.id??0,Time=Time,Position=Vec(hit.point),Normal=Vec(hit.normal),SurfaceId=prop.Definition.id,Color=color,Projectile=projectile});
        }
        private void Emit(EventKind kind,NativePilot source,int target,Vector3 position,float amount,string detail=null,float radius=0,Vector3? from=null,Vector3? normal=null,string surfaceId=null,int color=0,string damageKind=null,bool wallSlam=false,int projectile=0,bool pierced=false)
        {Events.Add(new ArenaEvent{Id=nextEvent++,Kind=kind,Time=Time,Source=source?.Seat.id??0,Target=target,Position=Vec(position),Amount=amount,Detail=detail,DamageKind=damageKind,WallSlam=wallSlam,Projectile=projectile,Pierced=pierced,Radius=radius,From=Vec(from??position),Normal=Vec(normal??Vector3.zero),SurfaceId=surfaceId,Color=color});}
        public Snapshot Snapshot(bool interpolated=false)
        {
            if(Replica&&replicaFrame!=null)return ReplicaSnapshot();
            foreach(var p in Pilots)
            {
                var a=p.Pose;a.BowDrawAt=p.BowDrawAt;a.BowDraw=p.BowDraw;a.BowReleaseAt=p.BowReleaseAt;a.BowRainAt=p.BowRainAt;a.BowRainTarget=Vec(p.BowRainTarget);a.Cyber=CyberSnapshot(p);a.Kernel=p.Kernel?.ViewCopy();a.Wounds=p.Wounds.ToArray();a.Weapon=p.Seat.spec=="pistol"?WeaponId.Pistol:p.Seat.spec=="rifle"?WeaponId.Rifle:p.Seat.spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;a.VisualSet=p.Seat.spec=="daggers"?1:p.Seat.spec=="greatsword"?2:p.Seat.spec=="shield"?3:0;a.MagicVisual=p.Seat.spec=="fire"?1:p.Seat.spec=="water"?2:p.Seat.spec=="lightning"?3:p.Seat.spec=="darkness"?4:0;a.SourceSpec=p.Seat.spec;a.BodyColor=p.Seat.bodyColor;a.AccentColor=p.Seat.accentColor;a.Hat=p.Seat.hat;a.HatColor=p.Seat.hatColor;a.SourceAction=p.Fighter?.Kind;a.SourceAt=p.Fighter?.At??0;a.SourceUntil=p.Fighter?.Until??0;a.SourceTotal=p.Fighter?.Total??0;a.SourceIndex=p.Fighter?.Index??0;a.RigScale=(p.Siren?.Scale??1)*p.Seat.devScale;
                if(p.Seat.spec=="cyberlord"){a.SourceRanged=p.Cyber.Ranged?.Ranged??false;a.SourceModeAt=p.Cyber.Ranged?.ModeAt??-100;}
                if(p.Seat.spec=="siren"){var m=p.Siren?.Move;a.SourceAction=NativeSirenRules.GrabPose(m?.Caught!=null,Time,m?.Until??0)?"grab":m?.Rule.Id;a.SourceAt=m?.Caught!=null?m.CaughtAt:m?.At??0;a.SourceUntil=m?.Until??0;a.SourceSide=m?.Side??1;a.SourceRanged=p.Siren?.Ranged??false;a.SourceModeAt=p.Siren?.ModeAt??-100;}
                var hold=p.HeldBy?.Siren?.Move;bool held=hold?.Caught==p&&p.Hp>0&&p.Seat.connected;
                a.GrabOwner=held?p.HeldBy.Seat.id:0;a.GrabAt=held?hold.CaughtAt:0;a.GrabCrushAt=held?hold.CrushAt:0;a.GrabUntil=held?hold.Until:0;
                a.Hp=p.Hp;a.Stamina=p.Stamina;a.Position=Vec(p.Body.position-Vector3.up*.865f);a.Velocity=Vec(p.Body.linearVelocity);a.Yaw=p.Yaw;a.AimYaw=p.Yaw;a.Pitch=p.Pitch;a.Combo=Math.Max(0,p.Combo);a.LastAttack=p.LastAttack;a.JuggleUntil=p.Juggle?.Until??0;a.AttackAt=p.AttackAt;a.AttackUntil=p.AttackEnd;a.BlockAt=p.BlockAt;a.BlockUntil=p.BlockEnd;a.BlockReady=p.BlockReady;a.RollAt=p.RollAt;a.RollDirection=Vec(p.RollDirection);a.StunUntil=p.StunEnd;a.OverdriveAt=p.OverdriveAt;a.OverdriveUntil=p.OverdriveUntil;a.VentUntil=p.VentUntil;a.Aiming=p.Aiming;a.AimAt=p.AimAt;a.Ammo=p.Ammo;a.LastShot=p.LastShot;a.ReloadAt=p.ReloadAt;a.ReloadUntil=p.ReloadEnd;
                CaptureMeleeKit(p);a.MeleeKit=p.MeleeKit.ViewCopy();
                var special=p.Special;var down=p.Down;
                a.SpecialKind=special?.Kind;a.SpecialAt=special?.At??-100;a.SpecialRelease=special?.Release??0;a.SpecialUntil=NativeMeleeKitRules.End(special);a.SpecialLanded=special?.Landed??-1;a.SpecialReleased=special?.Released??false;
                a.WeaponAway=p.Seat.spec=="shield"&&MeleeShots.Any(s=>s.Shield&&s.Original==p);a.MainWeaponAway=special!=null&&special.Kind=="dagger-throw"&&special.Released&&Time<special.Until-.12f;
                a.DownAt=down?.At??-100;a.DownUntil=down?.Until??0;a.DownLanded=down?.Landed??-1;a.DownPickup=down?.PickupUntil??0;a.DownUsed=down?.PickupUsed??false;a.DownRoll=down?.RollAt??-1;a.DownRollEnd=down?.RollUntil??-1;a.DownDirection=down?.Direction??new V3();
                MeleePresentation(p,a);CyberStepSnapshot(p,a);a.CastRate=(float)p.Stats["attackRate"];a.Grounded=p.Grounded;a.AirJumps=p.AirJumps;a.AirJumpAt=p.AirJumpAt;a.VisualWindup=p.Swing!=null?p.Swing.HitAt-p.Swing.At:(float)(p.Weapon.windup/p.Stats["attackRate"]);a.VisualActive=p.Swing!=null?p.Swing.HitUntil-p.Swing.HitAt:(float)(.16/p.Stats["attackRate"]);a.VisualDuration=p.AttackEnd-p.AttackAt;a.VisualRollDuration=p.RollEnd-p.RollAt;
            }
            return new Snapshot{Tick=(int)Math.Round(Match.Time*60),Actors=Pilots.Where(p=>p.Seat.connected).Select(p=>{if(!interpolated)return p.Pose;var shown=p.Pose.ViewCopy();shown.Position=Vec(p.Body.transform.position-Vector3.up*.865f);return shown;}).ToArray(),Covers=Array.Empty<Cover>(),Shots=Bullets.Select(b=>new Shot{Id=b.Id,Owner=b.Owner,Original=b.Original,Spec=b.Spec,Draw=b.Draw,Detail=b.Detail,SirenKind=b.SirenKind,SirenTarget=b.SirenTarget,LeetKind=b.LeetKind,Born=b.Born,Burn=b.Burn,Poison=b.Poison,Hadoken=b.Hadoken,Super=b.Super,Direct=b.Direct,ChipImpact=b.ChipImpact,WeaponFx=b.WeaponFx,Pong=b.Pong,Boomerang=b.Boomerang,Zigzag=b.Zigzag,Mods=b.Mods.Select(c=>new ChipCount{id=c.Key,count=c.Value}).ToArray(),Position=Vec(b.Position),Velocity=Vec(b.Velocity),Radius=b.Radius,MagicVisual=b.Spec=="fire"?1:b.Spec=="water"?2:b.Spec=="lightning"?3:b.Spec=="darkness"?4:0,Weapon=Content.Weapon(b.Spec).@class=="ranged"?WeaponId.Pistol:WeaponId.Sword}).ToArray(),Events=Events.ToArray()};
        }
        public void Dispose(){DevDispose();DestroyScene();foreach(var m in materials.Values)CapturedAnimation.Release(m);materials.Clear();foreach(var m in surfaces.Values)CapturedAnimation.Release(m);surfaces.Clear();}
    }
}
