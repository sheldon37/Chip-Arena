using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Cyber Lord's second kit (owner 2026-10-15: "fix Cyber Lord class- its current abilities are ... boring lol.
    /// Cyberlord isn't intended to be balanced, I want to have a very fun dev class to play. I like the melee "4" ability, but
    /// we need to change the other ones completely. Ranged is also intended to be like a cyber mage- throwing like
    /// "Cyber-themed fire balls" for their basic attack ... overhaul abilities except melee special attack "4" ... allow
    /// movement during ranged attacks on this class"). Claude's design and proposed numbers (docs/CYBER-LORD.md "Kit v2");
    /// Astra builds the rules, the wire record, prediction and bots. Until the v2 record lands, the v1 kit
    /// (CyberLordSpec, CyberRangedSpec) stays live and the presentation previews v2 in the sandbox. Not balanced on purpose:
    /// a DEV power fantasy.
    ///
    /// Both modes: Q Uplink (a grapple: zip to a surface, or yank a pilot in), E Overclock (a 6 s power state). Melee: the
    /// five-step Hardlight Combo, 1 Fork Bomb (three clones copying the swings, then detonating), 2 Glitch Cut (a dash-slash
    /// whose marks detonate together), 3 Meteor Byte (leap, aim, dive with a giant fist), 4 Kernel Panic (unchanged). Ranged
    /// (the cyber mage): Plasma Byte fireballs (every third an Overflow), 1 Daisy Chain (chain lightning), 2 Singularity (a
    /// pulling black hole that implodes), 3 Packet Storm (a meteor rain on an area), 4 Orbital Uplink (a steerable orbital
    /// laser; Kernel Panic's ultimate clock). Every ranged cast lets the pilot move (MoveScale).
    /// The pure helpers below (the storm's drops, the clones' formation, the chain's falloff, the meteor's arc, the orbital
    /// chase, the Overflow cadence) are the shared contract: the host resolves with them and the presentation draws with them,
    /// so every client agrees from the replicated starts alone.
    /// </summary>
    public static class CyberKit2Spec
    {
        public const int Version=2;
        /// <summary>Slots: Q and E (both modes), the melee LMB and 1-4, the ranged LMB and 1-4.</summary>
        public const int Q=0,E=1,MeleeLmb=2,Fork=3,Cut=4,Meteor=5,Panic=6,RangedLmb=7,Chain=8,Singularity=9,Storm=10,Orbital=11,Slots=12;
        public static readonly string[] Keys={"Q","E","LMB","1","2","3","4","LMB","1","2","3","4"};
        public static readonly string[] Names={"Uplink","Overclock","Hardlight Combo","Fork Bomb","Glitch Cut","Meteor Byte","Kernel Panic","Plasma Byte","Daisy Chain","Singularity","Packet Storm","Orbital Uplink"};
        public static readonly string[] Ids={"uplink","overclock","hardlight-combo","fork-bomb","glitch-cut","meteor-byte","kernel-panic","plasma-byte","daisy-chain","singularity","packet-storm","orbital-uplink"};
        public static readonly string[] Roles={"grapple","power state","combo","clones","dash strike","dive","ultimate","fireballs","chain","pull","area rain","ultimate"};
        /// <summary>Whether a slot belongs to the ranged set (the LMB and 1-4 after key 5).</summary>
        public static bool RangedSlot(int slot)=>slot>=RangedLmb&&slot<Slots;
        /// <summary>Cooldowns (s; the LMBs: the interval between attacks) and mana. Kernel Panic and Orbital Uplink share Kernel
        /// Panic's clock and cost (one ultimate clock for both 4s).</summary>
        public static readonly float[] Cooldowns={6,16,0,14,8,12,NativeKernelPanicRules.Cooldown,PlasmaInterval,6,15,12,NativeKernelPanicRules.Cooldown};
        public static readonly float[] Costs={10,30,0,35,20,35,NativeKernelPanicRules.Mana,0,20,40,35,NativeKernelPanicRules.Mana};
        /// <summary>Each cast's windup (Release = At + windup) and length (Until = At + length) at attack rate 1. The combo's
        /// own steps are in ComboWindups / ComboSeconds; Kernel Panic keeps NativeKernelPanicRules.</summary>
        public static readonly float[] Windup={.06f,.15f,0,.2f,.06f,.1f,NativeKernelPanicRules.Windup,PlasmaWindup,.12f,.2f,.25f,OrbitalCallDown};
        public static readonly float[] Seconds={.18f,.3f,0,.35f,.06f+CutSeconds,MeteorTotal,NativeKernelPanicRules.Windup,PlasmaInterval,.3f,.35f,.4f,OrbitalCallDown+OrbitalSeconds};
        /// <summary>The pilot's move speed share during a cast (1 full; Astra: no movement lock). Melee casts that move the body
        /// themselves (Glitch Cut, Meteor Byte, Kernel Panic) report 0: the host drives the body.</summary>
        public static readonly float[] MoveScale={1,1,0,1,0,0,0,1,1,1,1,OrbitalMove};
        /// <summary>The presentation's settle after a cast's end (not a rules window).</summary>
        public static readonly float[] Settle={.2f,.3f,0,.3f,.25f,.35f,0,.12f,.25f,.3f,.3f,.4f};
        /// <summary>Line icons in the class icon style (ClassIcons.json, tools/export_class_icons.mjs).</summary>
        public static readonly string[] Icons={"ability-uplink","ability-overclock","ability-hardlight-combo","ability-fork-bomb","ability-glitch-cut","ability-meteor-byte","ability-kernel-panic","ability-plasma-byte","ability-daisy-chain","ability-singularity","ability-packet-storm","ability-orbital-uplink"};
        public static int Slot(string id){for(int i=0;i<Ids.Length;i++)if(Ids[i]==id)return i;return -1;}

        // ---- Q Uplink (both modes) ----
        /// <summary>A hardlight cable along the aim: range and travel speed (m, m/s). On terrain the pilot zips to the anchor at
        /// ZipSpeed for at most ZipMax s, keeping the momentum (an attack or jump may follow at once). On an enemy it yanks them
        /// to YankStop m in front of the caster over YankSeconds and stuns them YankStun s; a boss is not moved (the caster
        /// zips to it instead). A miss retracts.</summary>
        public const float UplinkRange=24,CableSpeed=90,ZipSpeed=30,ZipMax=.8f,YankSeconds=.35f,YankStun=.6f,YankStop=2;
        // ---- E Overclock (both modes) ----
        /// <summary>A power state: its length, the extra move speed and attack rate shares, the cooldown recovery rate, the
        /// melee hit's chained arc (damage, hop range), and the ranged LMB's split (two side bolts at SplitAngle degrees, each
        /// SplitShare of the shot's damage).</summary>
        public const float OverclockSeconds=6,OverclockMove=.35f,OverclockRate=.6f,OverclockCooldownRate=2,ArcDamage=8,ArcRange=6,SplitAngle=7,SplitShare=.4f;

        // ---- melee LMB: the Hardlight Combo ----
        /// <summary>Five steps: jab, cross, spinning backfist (an arc round the pilot), rising uppercut (a short launch) and the
        /// Pulse Palm finisher (a cone shockwave). The first three keep v1's kind names so v1's records read the same.</summary>
        public const int ComboSteps=5;
        public static readonly string[] ComboKinds={"cyber-jab","cyber-cross","cyber-spin","cyber-rise","cyber-palm"};
        public static readonly float[] ComboSeconds={.24f,.26f,.32f,.34f,.42f},ComboWindups={.08f,.08f,.12f,.12f,.16f},ComboDamage={9,10,12,12,18};
        /// <summary>The idle reset, reach, arc (rad; the spin's is SpinArc), the uppercut's launch (m/s up; juggled RiseJuggle
        /// s) and the palm's cone and shove (m/s).</summary>
        public const float ComboReset=.7f,ComboRange=2.4f,ComboArc=1.4f,SpinArc=3.5f,RiseLaunch=6,RiseJuggle=.8f,PalmCone=1.6f,PalmShove=9;

        // ---- melee 1 Fork Bomb ----
        /// <summary>Three hardlight clones spawn round the caster and keep formation (ForkSlot) for ForkSeconds; each copies the
        /// caster's combo swings on the nearest enemy within CloneReach of it at CloneShare of the swing's damage. At the end
        /// (or on a recast) each dashes to its nearest enemy within DetonateRange over DetonateDash s and bursts.</summary>
        public const int Clones=3;
        public const float ForkSeconds=5,CloneShare=.35f,CloneReach=3,DetonateRange=10,DetonateDash=.3f,DetonateDamage=20,DetonateRadius=2.5f;
        /// <summary>Clone `i`'s formation point in the caster's frame (m: x right, z forward): back-left, back-right, behind.</summary>
        public static void ForkSlot(int i,out float x,out float z)
        {
            switch(i){case 0:x=-1.2f;z=-.6f;return;case 1:x=1.2f;z=-.6f;return;default:x=0;z=-1.4f;return;}
        }

        // ---- melee 2 Glitch Cut ----
        /// <summary>An instant dash-slash along the aim (path-constrained like v1's Data Dash) over CutSeconds; every enemy within
        /// CutWidth of the path is marked, and MarkDelay s after the dash ends all marks detonate together.</summary>
        public const float CutDistance=9,CutSeconds=.14f,CutWidth=1.2f,MarkDelay=.45f,CutDamage=25;

        // ---- melee 3 Meteor Byte ----
        /// <summary>A crouch (the windup), a leap of LeapHeight over LeapSeconds, a hover of HoverSeconds while the landing
        /// reticle follows the aim (within MeteorRange), then a dive of DiveSeconds to it: damage in MeteorRadius, a launch
        /// (m/s up; juggled MeteorJuggle s), a crater that lingers CraterSeconds (cosmetic).</summary>
        public const float LeapHeight=5,LeapSeconds=.45f,HoverSeconds=.25f,DiveSeconds=.2f,MeteorRange=14,MeteorRadius=4,MeteorDamage=35,MeteorLaunch=7,MeteorJuggle=1,CraterSeconds=2;
        public const float MeteorTotal=.1f+LeapSeconds+HoverSeconds+DiveSeconds;
        /// <summary>The body's height over the start at `u` 0..1 through the leap (eased out to the apex).</summary>
        public static float LeapArc(float u){u=Clamp01(u);return LeapHeight*(1-(1-u)*(1-u));}
        /// <summary>The dive's share of the way to the reticle at `u` 0..1 (accelerating: an impact, not a glide).</summary>
        public static float DiveShare(float u){u=Clamp01(u);return u*u;}

        // ---- ranged LMB Plasma Byte ----
        /// <summary>Cyber fireballs from alternating hands: interval, windup, speed, radius, direct and splash damage and splash
        /// radius, range, mana back per pilot hit. Every third throw (Overflow) is bigger.</summary>
        public const float PlasmaInterval=.3f,PlasmaWindup=.06f,PlasmaSpeed=38,PlasmaRadius=.22f,PlasmaDamage=10,PlasmaSplash=6,PlasmaSplashRadius=1.6f,PlasmaRange=45,PlasmaMana=3;
        public const float OverflowRadius=.35f,OverflowDamage=18,OverflowSplash=10,OverflowSplashRadius=2.6f;
        /// <summary>Whether throw number `shot` (0, 1, 2 ... since the attack chain began; a pause over ComboReset restarts it)
        /// is an Overflow.</summary>
        public static bool Overflow(int shot)=>shot>=0&&shot%3==2;
        /// <summary>The hand a throw leaves (0 right, 1 left): they alternate, the Overflow from both.</summary>
        public static int PlasmaHand(int shot)=>Overflow(shot)?2:shot%2;

        // ---- ranged 1 Daisy Chain ----
        /// <summary>A bolt to the aimed enemy (Range, assist cone in degrees), then up to Hops more within HopRange of the last,
        /// never the same pilot twice: Damage on the first, each hop Falloff less (multiplied), HopDelay s apart; each struck
        /// pilot slowed ChainSlow for ChainSlowSeconds.</summary>
        public const int Hops=5;
        public const float ChainRange=28,ChainCone=6,HopRange=8,ChainDamage=20,Falloff=.15f,HopDelay=.06f,ChainSlow=.3f,ChainSlowSeconds=.6f;
        public static float ChainHopDamage(int hop)=>hop<0||hop>Hops?0:ChainDamage*(float)Math.Pow(1-Falloff,hop);

        // ---- ranged 2 Singularity ----
        /// <summary>A thrown orb (speed, range; it stops on the first surface or pilot), then a black hole for HoleSeconds pulling
        /// enemies within PullRadius toward it (PullSpeed m/s at the edge, doubling at the core), ticking TickDamage every
        /// HoleTick, then an implosion (damage in ImplodeRadius, shoved outward at ImplodeShove m/s).</summary>
        public const float OrbSpeed=18,OrbRange=24,HoleSeconds=2.5f,PullRadius=7,PullSpeed=6,HoleTick=.5f,TickDamage=5,ImplodeDamage=40,ImplodeRadius=4,ImplodeShove=8;
        /// <summary>The pull speed at distance `d` from the core (0 outside PullRadius).</summary>
        public static float Pull(float d)=>d<0||d>=PullRadius?0:PullSpeed*(2-d/PullRadius);

        // ---- ranged 3 Packet Storm ----
        /// <summary>A ring at the aimed floor point (within StormRange) of StormRadius; Drops meteors fall over StormSeconds from
        /// the release, each seen falling FallSeconds before its impact (DropDamage in DropRadius).</summary>
        public const int Drops=10;
        public const float StormRange=30,StormRadius=6,StormSeconds=1.6f,FallSeconds=.35f,DropDamage=14,DropRadius=2;
        /// <summary>Drop `i` of a storm with `seed`: its impact time after the release and its point in the ring (m, about the
        /// centre). Pure: the host and every client derive the same drops from the replicated seed.</summary>
        public static void StormDrop(int seed,int i,out float at,out float x,out float z)
        {
            float slot=StormSeconds/Drops;at=Math.Max(0,i*slot+slot*.5f*(Hash(seed*31+i*7+1)*.5f+.5f));
            float r=StormRadius*(float)Math.Sqrt(.15f+.85f*(Hash(seed*31+i*7+2)*.5f+.5f)),a=(float)Math.PI*(Hash(seed*31+i*7+3)+1);x=r*(float)Math.Cos(a);z=r*(float)Math.Sin(a);
        }

        // ---- ranged 4 Orbital Uplink ----
        /// <summary>The call-down (a targeting ring opens at the aim's floor point), then an orbital column for OrbitalSeconds of
        /// OrbitalRadius that chases the aim's floor point (within OrbitalRange of the caster) at OrbitalChase m/s: OrbitalDps
        /// per second in OrbitalTick ticks, enemies inside slowed OrbitalSlow; the caster moves at OrbitalMove while steering.
        /// A stun or death ends it.</summary>
        public const float OrbitalCallDown=.6f,OrbitalSeconds=4,OrbitalRadius=2.2f,OrbitalChase=9,OrbitalRange=35,OrbitalDps=30,OrbitalTick=.25f,OrbitalSlow=.4f,OrbitalMove=.6f;
        /// <summary>The column's floor point after `dt` s chasing `aim` from `at` (x, z; it never overshoots).</summary>
        public static void OrbitalStep(ref float atX,ref float atZ,float aimX,float aimZ,float dt)
        {
            float dx=aimX-atX,dz=aimZ-atZ,d=(float)Math.Sqrt(dx*dx+dz*dz),step=OrbitalChase*Math.Max(0,dt);if(d<=step||d<1e-6f){atX=aimX;atZ=aimZ;return;}atX+=dx/d*step;atZ+=dz/d*step;
        }

        /// <summary>A signed hash in [-1, 1] (deterministic on every client).</summary>
        public static float Hash(int n){uint x=unchecked((uint)n*2654435761u);x^=x>>15;x=unchecked(x*2246822519u);x^=x>>13;return (x&0xffff)/32767.5f-1;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }
}
