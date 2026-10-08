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
        public float X, Z, Yaw, Pitch;
        public byte SelectWeapon; // 0=no change, 1=Sword, 2=Pistol, 3=Rifle, 4=MachineGun
        public V3 AimTarget,AbilityTarget;
        public bool Interact;public bool Hadoken,HadokenHeld,Uppercut;public byte SirenAbility;public bool Attack, Block, Roll, Shoot, Jump, Sprint, Aim, Reload, AbilityQ, AbilityE, AbilityEHeld, AbilityEReleased, CancelAbilities;
        public bool Valid { get { return Sequence >= 0 && MathUtil.Finite(X) && MathUtil.Finite(Z) && MathUtil.Finite(Yaw) && MathUtil.Finite(Pitch) && MathUtil.Finite(AbilityTarget.x) && MathUtil.Finite(AbilityTarget.y) && MathUtil.Finite(AbilityTarget.z) && AbilityTarget.Length<=10000 && MathUtil.Finite(AimTarget.x) && MathUtil.Finite(AimTarget.y) && MathUtil.Finite(AimTarget.z) && AimTarget.Length<=10000 && SelectWeapon<=4 && SirenAbility<=4; } }
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
        public NativeWound[] Wounds=Array.Empty<NativeWound>();public string SourceSpec,SourceAction,BodyColor="#e5e7eb",AccentColor="#70e1d0",Hat="none";public float SourceAt,SourceUntil,RigScale=1;public int SourceIndex,SourceTotal,SourceSide=1;public bool SourceRanged;
        // Native authority presentation only; the inherited lab wire codec does not transport these.
        public string SpecialKind;public float SpecialAt=-100,SpecialRelease,SpecialUntil,SpecialLanded=-1;
        public bool SpecialReleased,WeaponAway,DownUsed;public float DownAt=-100,DownUntil,DownLanded=-1,DownPickup,DownRoll=-1,DownRollEnd=-1;public V3 DownDirection;
        public float CastRate=1,AbilityAt=-100,AbilityRelease;public string AbilityId;
        public bool Grounded=true;
        public float VisualWindup=Rules.SwordWindup,VisualActive=Rules.SwordActive,VisualDuration=Rules.SwordDuration,VisualRollDuration=Rules.RollDuration;
        public int Id, Team, Combo, Action, Kills, LastInput;
        public WeaponId Weapon;
        public int Ammo, ShotsFired, AimShots;
        public bool Aiming;
        public float Pitch, AimYaw, AimAt=-100, LastShot=-100, ReloadAt=-100, ReloadUntil;
        internal readonly int[] Magazines={0,7,3,48};
        public bool Bot, Sprinting, SprintExhausted;
        public float Hp = 100, Stamina = 100, Yaw;
        public V3 Position, Velocity, Knockback, RollDirection;
        public float AttackAt = -100, AttackUntil = -100, BlockAt=-100, BlockUntil, BlockReady, RollAt = -100, RollReady;
        public float StunUntil, StaggerImmuneUntil, RegenAt, ShootReady, RespawnAt, FlashUntil;
        internal bool QueuedAttack;
        internal readonly HashSet<int> Hit = new HashSet<int>();
        public Actor ViewCopy() => (Actor)MemberwiseClone();
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
        public V3 Position, Velocity;
        public float Damage = 20, Life = 3, Gravity, Radius=.1f, DistanceLeft=320, Impulse=325;
        public WeaponId Weapon;
    }

    public enum EventKind { Hit, Block, Dodge, Shot, Death, Break, ReloadStart, ReloadEnd, Headshot, Impact }
    [Serializable]
    public struct ArenaEvent
    {
        public int Id, Source, Target;
        public EventKind Kind;
        public V3 Position;
        public float Amount;
        // Native surface FX payload; the retained lab wire format is unchanged.
        public float Time;public V3 Normal;public string SurfaceId;public int Color,Projectile;
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
