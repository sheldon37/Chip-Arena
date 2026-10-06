using System;
using System.Collections.Generic;

namespace ChipArena.UnityPrototype
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
        public float X, Z, Yaw;
        public bool Attack, Block, Roll, Shoot, Jump, Sprint;
        public bool Valid { get { return Sequence >= 0 && MathUtil.Finite(X) && MathUtil.Finite(Z) && MathUtil.Finite(Yaw); } }
        public InputFrame Sanitized()
        {
            if (!Valid) return new InputFrame();
            InputFrame input = this;
            V3 move = new V3(X, 0, Z); if (move.Length > 1) move = move.Normalized;
            input.X = move.x; input.Z = move.z; input.Yaw = Yaw % 360; return input;
        }
        public InputFrame HeldOnly() { InputFrame i = this; i.Attack = i.Block = i.Roll = i.Jump = false; return i; }
    }

    [Serializable]
    public class Actor
    {
        public int Id, Team, Combo, Action, Kills, LastInput;
        public bool Bot, Sprinting, SprintExhausted;
        public float Hp = 100, Stamina = 100, Yaw;
        public V3 Position, Velocity, Knockback, RollDirection;
        public float AttackAt = -100, AttackUntil = -100, BlockUntil, BlockReady, RollAt = -100, RollReady;
        public float StunUntil, StaggerImmuneUntil, RegenAt, ShootReady, RespawnAt, FlashUntil;
        internal bool QueuedAttack;
        internal readonly HashSet<int> Hit = new HashSet<int>();
        public bool Alive { get { return Hp > 0; } }
        public bool Rolling(float t) { return t >= RollAt && t < RollAt + Rules.RollDuration; }
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
        public int Id, Owner, Team, Reflections;
        public V3 Position, Velocity;
        public float Damage = 20, Life = 3;
    }

    public enum EventKind { Hit, Block, Dodge, Shot, Death, Break }
    [Serializable]
    public struct ArenaEvent
    {
        public int Id, Source, Target;
        public EventKind Kind;
        public V3 Position;
        public float Amount;
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
