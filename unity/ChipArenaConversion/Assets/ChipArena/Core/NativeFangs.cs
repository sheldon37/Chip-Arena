using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Fangs' names and numbers (docs/FANGS.md): the owner's DEV dagger class of 2026-11-10 ("DEV-Only Daggers ... Testing different
    /// abilities and animation sets"). Twin curved venom daggers on the Frank Slash Pack's assassin set: five quick cuts that each leave a
    /// venom stack (the arena's poison, stronger with each stack), a pounce on a sprint, Q a shadow step behind the foe nearest the aim with a
    /// stab, E a smoke veil (unseen by enemies until the next attack, which strikes twice as hard), 1 a fan of thrown venom knives, 2 the
    /// venom blooms (every stack bursts at once), 3 an assassination: a paired takedown from behind or on a weakened foe (the foe held in
    /// the matching clip), lethal at its blow. The rules (Runtime NativeFangsAuthority.cs) and the presentation both read these. Pure.
    /// </summary>
    public static class NativeFangs
    {
        public const string Spec="fangs",Name="Fangs";
        public const string Cut="fangs-cut",Pounce="fangs-pounce",Step="fangs-step",Veil="fangs-veil",Fan="fangs-fan",Bloom="fangs-bloom",Assassinate="fangs-assassinate";
        /// <summary>The class's own entrance (Core NativeFangsIntro).</summary>
        public const string Entrance="fangs-silent-kill";
        // Hit kinds: a cut (Amount: the venom stacks after it), the finisher, the pounce, the shadow step's vanish and its stab, the veil's
        // smoke, a knife's flight start and its hit, the bloom (on each poisoned one, Amount: the stacks burst), the takedown's blow and kill.
        public const string HitCut="f-cut",HitFinisher="f-finish",HitPounce="f-pounce",HitVanish="f-vanish",HitStab="f-stab",HitSmoke="f-smoke",HitKnife="f-knife",HitThrow="f-throw",HitBloom="f-bloom",HitTakedown="f-takedown",HitKill="f-kill";
        // The chips' (Core NativeFangsChips): a venom drip, a spread of venom, a drain back to the wielder, a smoke cloud's pulse.
        public const string HitDrip="f-drip",HitSpread="f-spread",HitDrain="f-drain",HitCloud="f-cloud";
        public const int Slots=5;
        public static readonly string[] SlotKeys={"Q","E","1","2","3"};
        /// <summary>Cooldowns (s) in slot order: Q the step, E the veil (from its end), 1 the fan, 2 the bloom, 3 the assassination.</summary>
        public static readonly float[] Cooldown={7,12,6,10,18};

        // ---- the combo ----
        /// <summary>The five cuts: the wind-up to the hit (s), the whole move (s), damage, reach (m), half arc (rad), the step in (m) and the venom
        /// each leaves.</summary>
        public static readonly float[] CutHit={.1f,.1f,.12f,.12f,.16f},CutLength={.3f,.3f,.36f,.38f,.52f},CutDamage={10,10,11,12,18},CutReach={2.1f,2.1f,2.2f,2.2f,2.6f},CutArc={1.2f,1.2f,1.2f,1.4f,1f},CutLunge={0,0,.5f,0,1.2f};
        public static readonly int[] CutVenom={1,1,1,1,2};
        public const float ComboWindow=.4f;
        public const int Steps=5;
        // ---- venom (the arena's poison, by stacks) ----
        /// <summary>Up to VenomMax stacks on a foe, each VenomDps a second, every stack's clock refreshed to VenomFor by a new one.</summary>
        public const int VenomMax=6;public const float VenomDps=1.6f,VenomFor=4;
        // ---- the pounce (a sprint's attack) ----
        public const float PounceWind=.1f,PounceTime=.32f,PounceReach=5,PounceRadius=1.6f,PounceDamage=20,PounceRecover=.3f;public const int PounceVenom=2;
        // ---- the shadow step (Q) ----
        /// <summary>Gone for StepVanish s, then behind the foe nearest the aim (within StepRange m and StepCone of the look, in sight), StepBehind m
        /// behind it and facing it, and a stab (StepDamage, StepVenom); with no foe, StepBlink m along the look.</summary>
        public const float StepVanish=.14f,StepRange=12,StepCone=35,StepBehind=1.15f,StepBlink=6,StepDamage=24,StepRecover=.3f;public const int StepVenom=2;
        // ---- the veil (E) ----
        /// <summary>Smoke, and unseen by enemies for VeilFor s or until an attack; that attack deals VeilBonus times and leaves VeilVenom more.</summary>
        public const float VeilCast=.2f,VeilFor=4,VeilBonus=2;public const int VeilVenom=3;
        // ---- the fan (1) ----
        /// <summary>FanKnives knives across FanSpread degrees at FanSpeed m/s out to FanRange m, each FanDamage and a venom stack.</summary>
        public const int FanKnives=5;public const float FanRelease=.2f,FanRecover=.25f,FanSpread=30,FanSpeed=34,FanRange=18,FanDamage=9,FanRadius=.45f;
        // ---- the bloom (2) ----
        /// <summary>Every poisoned foe within BloomRange m: BloomPerStack per venom stack at once, the stacks spent.</summary>
        public const float BloomRelease=.3f,BloomRecover=.3f,BloomRange=14,BloomPerStack=6;
        // ---- the assassination (3) ----
        /// <summary>A foe within KillReach m in front: from behind (its back to you within KillBehind degrees) or below KillThreshold of its
        /// health, the takedown: both held through it, lethal at its blow (KillBoss against a boss); otherwise a frontal critical (KillFront).</summary>
        public const float KillReach=2.4f,KillBehind=75,KillThreshold=.4f,KillBoss=60,KillFront=45,KillCone=55;
        /// <summary>The takedown from behind and the frontal critical: their held span and blow (s after the cast), and how far in front of the
        /// wielder the foe is held.</summary>
        public const float BehindHit=.95f,BehindHold=1.45f,FrontHit=.6f,FrontHold=1.2f,PairGap=.85f;

        public static string AbilityName(string id)
        {
            switch(id){case Cut:return "Fang Flurry";case Pounce:return "Pounce";case Step:return "Shadow Step";case Veil:return "Smoke Veil";case Fan:return "Fang Fan";case Bloom:return "Venom Bloom";case Assassinate:return "Assassinate";default:return "";}
        }
        public static string Icon(string id)=>id==Step?"footprints":id==Veil?"ghost":id==Fan?"feather":id==Bloom?"biohazard":id==Assassinate?"skull":id==Pounce?"move-up":"swords";
        public static readonly string[] SlotMoves={Step,Veil,Fan,Bloom,Assassinate};
        public static bool IsCast(string k)=>k==Cut||k==Pounce||k==Step||k==Veil||k==Fan||k==Bloom||k==Assassinate;
        public static bool IsHit(string k)=>k==HitCut||k==HitFinisher||k==HitPounce||k==HitVanish||k==HitStab||k==HitSmoke||k==HitKnife||k==HitThrow||k==HitBloom||k==HitTakedown||k==HitKill||k==HitDrip||k==HitSpread||k==HitDrain||k==HitCloud;
        /// <summary>The fan's knife `i` of `n` across `spread` degrees about the flat look `dir`.</summary>
        public static Vector3 KnifeDir(Vector3 dir,int i,int n,float spread)
        {
            var d=new Vector3(dir.X,0,dir.Z);float l=d.Length();d=l>1e-4f?d/l:Vector3.UnitZ;float a=n<=1?0:(i/(float)(n-1)-.5f)*spread*(float)Math.PI/180;
            float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(d.X*c+d.Z*s,0,-d.X*s+d.Z*c);
        }
        /// <summary>Whether `wielder` stands behind `target` (looking along `targetFacing`, flat): the target's back within KillBehind degrees.</summary>
        public static bool Behind(Vector3 wielder,Vector3 target,Vector3 targetFacing)
        {
            var to=new Vector3(wielder.X-target.X,0,wielder.Z-target.Z);float l=to.Length();if(l<1e-4f)return false;to/=l;var f=new Vector3(targetFacing.X,0,targetFacing.Z);float fl=f.Length();if(fl<1e-4f)return false;f/=fl;
            return Vector3.Dot(to,-f)>=(float)Math.Cos(KillBehind*Math.PI/180);
        }
    }
}
