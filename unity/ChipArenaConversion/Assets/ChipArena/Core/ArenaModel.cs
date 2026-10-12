using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public static class Rules
    {
        // Selected values from shared commit 05950ea; see PROTOTYPE-SCOPE.md for deliberate differences.
        public const float Dt = 1f / 60, Speed = 6.8f, Acceleration = 35, Gravity = 23, JumpSpeed = 8.5f;
        public const float Radius = .42f, StaminaRegen = 24, RegenDelay = .45f, SprintMultiplier = 1.35f, SprintDrain = 12;
        public const float BlockDuration = .30f, BlockCooldown = 4, SwordCost = 14, SwordDamage = 30;
        public const float SwordWindup = .14f, SwordActive = .16f, SwordDuration = .62f, SwordReach = 2.5f;
        public const float RollDuration = .4625f, RollDistance = 5.3f, RollCost = 24, RollCooldown = .75f;
        public const float IframeStart = .075f, IframeEnd = .255f, ArenaHalf = 24;
        public const int MaxBots = 192, MaxProjectiles = 1024;
    }

    [Serializable]
    public struct InputFrame
    {
        public int Sequence;
        public int LockTarget; // Intent only: native authority rechecks enemy, range and line of sight.
        public float X, Z, Yaw, Pitch;
        public byte SelectWeapon; // 0=no change, 1=Sword, 2=Pistol, 3=Rifle, 4=MachineGun
        public V3 AimTarget,AbilityTarget;
        public bool Interact;public bool Hadoken,HadokenHeld,Uppercut;public byte SirenAbility;public bool Attack, Block, Roll, Shoot, Jump, Sprint, Aim, Reload, AbilityQ, AbilityE, AbilityEHeld, AbilityEReleased, CancelAbilities;
        public bool Valid { get { return Sequence >= 0 && LockTarget>=0 && LockTarget<10000000 && MathUtil.Finite(X) && MathUtil.Finite(Z) && MathUtil.Finite(Yaw) && MathUtil.Finite(Pitch) && MathUtil.Finite(AbilityTarget.x) && MathUtil.Finite(AbilityTarget.y) && MathUtil.Finite(AbilityTarget.z) && AbilityTarget.Length<=10000 && MathUtil.Finite(AimTarget.x) && MathUtil.Finite(AimTarget.y) && MathUtil.Finite(AimTarget.z) && AimTarget.Length<=10000 && SelectWeapon<=4 && SirenAbility<=4; } }
        public InputFrame Sanitized()
        {
            if (!Valid) return new InputFrame();
            InputFrame input = this;
            V3 move = new V3(X, 0, Z); if (move.Length > 1) move = move.Normalized;
            input.X = move.x; input.Z = move.z; input.Yaw = Yaw % 360; input.Pitch=MathUtil.Clamp(Pitch,-75,75); return input;
        }
        public InputFrame HeldOnly() { InputFrame i = this; i.Attack = i.Block = i.Roll = i.Jump = i.Reload = false; i.AbilityQ=i.AbilityE=i.AbilityEReleased=i.CancelAbilities=false; i.SelectWeapon=0;i.Hadoken=i.Uppercut=i.Interact=false;i.SirenAbility=0; return i; }
    }

    [Serializable]
    public class Actor
    {
        public int VisualSet,MagicVisual;
        // Last Laugh victim identity/timeline, consumed by animation, flames and both participant cameras on every peer.
        public int GrabOwner;public float GrabAt,GrabCrushAt,GrabUntil;
        public NativeWound[] Wounds=Array.Empty<NativeWound>();public string SourceSpec,SourceAction,BodyColor="#e5e7eb",AccentColor="#70e1d0",Hat="none";public float SourceModeAt=-100;public float SourceAt,SourceUntil,RigScale=1;public int SourceIndex,SourceTotal,SourceSide=1;public bool SourceRanged;
        // The headwear colour (NativeHatLooks), filled from the seat on host and guests alike: never on the wire per tick.
        [NonSerialized] public string HatColor="";
        // PROVISIONAL bow presentation fixture slot (Core/NativeBow.cs NativeBowWire): never on the wire; replaced by the simulation's fields.
        [NonSerialized] public NativeBowFeed Bow;
        // Native authority presentation only; the inherited lab wire codec does not transport these.
        public NativeMeleeKit MeleeKit;public string SpecialKind;public float SpecialAt=-100,SpecialRelease,SpecialUntil,SpecialLanded=-1;
        public bool SpecialReleased,WeaponAway,DownUsed;public float DownAt=-100,DownUntil,DownLanded=-1,DownPickup,DownRoll=-1,DownRollEnd=-1;public V3 DownDirection;
        public float CastRate=1,AbilityAt=-100,AbilityRelease;public string AbilityId;
        public bool Grounded=true;public int AirJumps;public float AirJumpAt=-100;
        // Captured-melee presentation (MeleeMotion): the swing window, authoritative step and commitment, Hurricane, and the
        // hit reaction in the struck pilot's facing frame (source frame: +x the rig's left, radians). Never read by authority.
        public float SwingAt=-100,SwingUntil=-100,CommitAt=-100,CommitUntil=-100,CommitYaw,StepAt,StepUntil,StepDistance;public bool Followup,Hurricane,SprintAttack,OpenerLunge;
        public bool AirAttack,AirAttackUsed,MainWeaponAway;public float PlungeAt,ImpactAt=-1,AirborneAt=-1;public int SwingRemaining;public float ChainAt,MeleeBufferUntil=-1,ComboUntil;
        public float ReactionAt=-100,ReactionUntil,ReactionYaw,ReactionTurn,ReactionStrength;public V3 ReactionDir,ReactionPoint;public int ReactionZone;public bool ReactionParry;
        public float VisualWindup=Rules.SwordWindup,VisualActive=Rules.SwordActive,VisualDuration=Rules.SwordDuration,VisualRollDuration=Rules.RollDuration;
        public int Id, Team, Combo, Action, Kills, LastInput;
        public WeaponId Weapon;
        public int Ammo, ShotsFired, AimShots;
        public float OverdriveAt,OverdriveUntil,VentUntil;
        public bool Aiming;
        public float Pitch, AimYaw, AimAt=-100, LastShot=-100, ReloadAt=-100, ReloadUntil;
        internal readonly int[] Magazines={0,7,3,48};
        public bool Bot, Sprinting, SprintExhausted;
        public float Hp = 100, Stamina = 100, Yaw;
        public V3 Position, Velocity, Knockback, RollDirection;
        public float LastAttack=-100,JuggleUntil;public float AttackAt = -100, AttackUntil = -100, BlockAt=-100, BlockUntil, BlockReady, RollAt = -100, RollReady;
        public float StunUntil, StaggerImmuneUntil, RegenAt, ShootReady, RespawnAt, FlashUntil;
        internal bool QueuedAttack;
        internal readonly HashSet<int> Hit = new HashSet<int>();
        public Actor ViewCopy() {var copy=(Actor)MemberwiseClone();copy.MeleeKit=MeleeKit?.ViewCopy();return copy;}
        public bool Alive { get { return Hp > 0; } }
        public bool Rolling(float t) { return t >= RollAt && t < RollAt + VisualRollDuration; }
        public bool Invulnerable(float t) { return t >= RollAt + Rules.IframeStart && t < RollAt + Rules.IframeEnd; }
        public bool Attacking(float t) { return t >= AttackAt && t < AttackUntil; }
    }

    [Serializable]
    public class Cover
    {
        public int Id;
        public V3 Position;
        public float HalfX = 1, HalfZ = 1, Height = 2, Hp = 85;
    }

    [Serializable]
    public class Shot
    {
        public int Id, Owner, Team, Reflections, MagicVisual;
        public string Spec,SirenKind,LeetKind;public int Original,SirenTarget;public float Born,Burn,Poison;
        public bool Hadoken,Super,Direct,ChipImpact,WeaponFx,Pong,Boomerang,Zigzag;public ChipCount[] Mods=Array.Empty<ChipCount>();
        public V3 Position, Velocity;
        public float Damage = 20, Life = 3, Gravity, Radius=.1f, DistanceLeft=320, Impulse=325;
        public WeaponId Weapon;
    }

    public enum EventKind { Hit, Block, Dodge, Shot, Death, Break, ReloadStart, ReloadEnd, Headshot, Impact, Explosion, BlockPulse, Vacuum, Arc, Freeze, Teleport, Critical, Catch, Split, Ricochet, Pong, CeilingHit, HazardHit, BurnTick, PoisonTick, Collapse, Leet, Emperor, Arcade, Siren, PveBuy, PveGib, PvePhase, PveRevive, PveMoney, Install, Field, Summon, Heal, StructureStress, StructureFall, StructureLand, AbilityCast, AbilityHit, Mark, Recast, Absorb, Bounce, Land, FissureStart }
    [Serializable]
    public struct ArenaEvent
    {
        public int Id, Source, Target;
        public EventKind Kind;
        public V3 Position;
        public float Amount;
        // Native surface FX payload; the retained lab wire format is unchanged.
        public bool WallSlam;public string Detail,DamageKind;public float Radius;public V3 From;public float Time;public V3 Normal;public string SurfaceId;public int Color,Projectile;
    }

    [Serializable]
    public class Snapshot
    {
        public int Tick;
        public Actor[] Actors;
        public Cover[] Covers;
        public Shot[] Shots;
        public ArenaEvent[] Events;
    }
}
