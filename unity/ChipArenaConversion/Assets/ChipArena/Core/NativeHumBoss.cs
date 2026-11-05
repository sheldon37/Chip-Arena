using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM, Level 0's boss (docs/EXPEDITION-BOSS-BACKROOMS.md section 3): what has been wandering nearby. The names the rest of the game
    /// reads, the intro's shots and card, and the design as data: the phases, the move table (<see cref="Moves"/>, the table Astra's rules read),
    /// the anti-camp meter, the weak points and the hit volumes. Presentation only until Astra binds the record: nothing here is read by a rule yet,
    /// and every number is a proposal Astra owns.
    /// Frame: +z the way it faces, +y up, +x its right; the root on the floor under its middle.
    /// </summary>
    public static class NativeHumBoss
    {
        public const string Archetype="level0-hum",Name="THE HUM",Area=NativeBackroomsGenerator.AreaId,Map=NativeBackroomsGenerator.ArenaId;
        public static readonly BossIntroCard Card=new BossIntroCard{Eyebrow="BOSS · LEVEL 0",Title="THE HUM",Line="IT HAS HEARD YOU",Ink=0x060502,TopLine=0xfff2c0,Rule=0xffe9a0,BarFrom=0xc8a820,BarTo=0xffffe8,Letters=0xfffef0,Glow=0xffe680,SplitA=0xffd040,SplitB=0xf4fff0,Shades=.42f};
        /// <summary>The three phases and the health fraction each starts at.</summary>
        public const int Phases=3;
        public static readonly string[] PhaseNames={"HUM-BUZZ","BLACKOUT","NOCLIP"};
        public static readonly float[] PhaseFrom={1f,.65f,.30f};
        /// <summary>A phase change's scripted window: the boss is busy (proposed invulnerable) while the room changes.</summary>
        public const float PhaseChangeSeconds=3.5f;
        /// <summary>The phase a health fraction falls in (1..3).</summary>
        public static int Phase(float health){int p=1;for(int i=1;i<Phases;i++)if(health<=PhaseFrom[i])p=i+1;return p;}

        /// <summary>Fall Through's meter (doc section 3): seconds within <see cref="StillRadius"/> of one spot (or over <see cref="HighAbove"/> m up for
        /// <see cref="HighSeconds"/>) before the carpet goes, by phase: the anti-camp rule. Movement inside the radius doesn't reset it.</summary>
        public const float StillRadius=3,StillSeconds=6,StillSecondsLast=4,HighAbove=1.6f,HighSeconds=2;
        public static float StillFor(int phase)=>phase>=3?StillSecondsLast:StillSeconds;
        /// <summary>The 6:00 enrage: Max Hum-Buzz, a pulse every 5 s.</summary>
        public const float EnrageAt=360,EnragePulse=5;
        /// <summary>The Peripheral Shift's period by phase, and the proposed health multiplier over NativePveRules.BossHp.</summary>
        public static readonly float[] ShiftEvery={18,12,8};
        public const float HealthScale=1.1f;
        /// <summary>A Noclip lunge that ends this close to a surface leaves it half inside it for <see cref="ClippedSeconds"/>, taking 2x.</summary>
        public const float ClipReach=1,ClippedSeconds=2.2f;
        /// <summary>Breaks: a tube's share of max health, the head and the Ballast's multipliers, and three broken in a charge staggers it.</summary>
        public const float TubeShare=.025f,HeadScale=2,BallastScale=1.5f,BallastOpenScale=2.5f,ChokeStagger=2;public const int ChokeTubes=3,TubeCount=7;
        /// <summary>The proposed gap between moves (s): it may overlap one big and one small move.</summary>
        public const float MoveGap=1;

        /// <summary>
        /// The moves (the doc's table, seconds, and damage before party scaling): wind (the telegraph), strike (the active window; travel for
        /// the lunge), follow-through, recover; then the cooldown (0 for a meter move). `Reach` the arc's or lane's length, `Radius` a circle's,
        /// `Arc` degrees, `Width` a lane's, `Speed` the lunge's or ring's. `Grab` a grab's reach from a surface, `PerSecond` its damage while held.
        /// </summary>
        public static readonly HumBossMove[] Moves={
            new HumBossMove{Id="reach",Title="Reach",Phase=1,Wind=.6f,Strike=.2f,Follow=.3f,Recover=.5f,Cooldown=3,Damage=22,Reach=6,Arc=130},
            new HumBossMove{Id="stoop",Title="Stoop",Phase=1,Wind=.7f,Strike=.15f,Follow=.4f,Recover=.6f,Cooldown=7,Damage=30,Radius=4.5f},
            new HumBossMove{Id="row",Title="Row Burst",Phase=1,Wind=1.1f,Strike=.1f,Follow=.5f,Recover=.4f,Cooldown=10,Damage=16,Width=1.4f},
            new HumBossMove{Id="lunge",Title="Noclip",Phase=1,Wind=1.2f,Strike=1.2f,Follow=.4f,Recover=.7f,Cooldown=12,Damage=34,Reach=26,Width=3,Speed=22},
            new HumBossMove{Id="tear",Title="Fall Through",Phase=1,Wind=1.5f,Strike=.2f,Follow=0,Recover=0,Cooldown=0,Damage=12,Radius=2.4f},
            new HumBossMove{Id="shift",Title="Peripheral Shift",Phase=1,Wind=0,Strike=0,Follow=0,Recover=0,Cooldown=18,Damage=0},
            new HumBossMove{Id="pulse",Title="Max Hum",Phase=2,Wind=1.4f,Strike=1.3f,Follow=.3f,Recover=.8f,Cooldown=14,Damage=14,Speed=14},
            new HumBossMove{Id="cut",Title="Lights Out",Phase=2,Wind=1.2f,Strike=.3f,Follow=.4f,Recover=.6f,Cooldown=16,Damage=38,Reach=3.5f,Arc=180},
            new HumBossMove{Id="hand",Title="Wall Hand",Phase=2,Wind=.8f,Strike=.25f,Follow=1.5f,Recover=.6f,Cooldown=15,Damage=8,Grab=2.5f,PerSecond=8},
            new HumBossMove{Id="voices",Title="Familiar Voices",Phase=2,Wind=1,Strike=2,Follow=0,Recover=.5f,Cooldown=25,Damage=0}};
        public static HumBossMove Move(string id){foreach(var m in Moves)if(m.Id==id)return m;return default;}
        public static int MoveIndex(string id){for(int i=0;i<Moves.Length;i++)if(Moves[i].Id==id)return i;return -1;}

        /// <summary>The measures (doc section 3): the back of the stoop under the 4 m ceiling, the head fixture's centre and size, the arm from shoulder
        /// to knuckle, the leg, and its height when it rears through the ceiling.</summary>
        public const float BackHeight=3.95f,HeadHeight=3.3f,HeadWidth=1.6f,HeadTall=.4f,HeadDeep=.3f,ArmLength=3.4f,LegLength=2.2f,RearHeight=5.6f;

        /// <summary>The proposed hit volumes in the root's frame at the neutral stoop (Astra's colliders should follow the rig's joints where they break:
        /// <see cref="HumBossWeakPoint"/>): the body's capsule from the sacrum to the chest; the head and the Ballast as boxes round their joints; the arms' reach.</summary>
        public static readonly Vector3 BodyA=new Vector3(0,2.05f,-.62f),BodyB=new Vector3(0,3.5f,-.06f);public const float BodyRadius=.5f;
        /// <summary>The weak points as data (doc section 3): what breaks or takes extra damage, the joint it rides, its half size and multiplier.</summary>
        public static readonly HumBossWeakPoint[] WeakPoints=MakeWeakPoints();
        private static HumBossWeakPoint[] MakeWeakPoints()
        {
            var w=new HumBossWeakPoint[TubeCount+2];
            w[0]=new HumBossWeakPoint{Id="head",Joint=NativeHumBossLook.Head,Half=new Vector3(.8f,.2f,.15f),Scale=HeadScale,Breaks=true};
            w[1]=new HumBossWeakPoint{Id="ballast",Joint=NativeHumBossLook.Ballast,Half=new Vector3(.24f,.2f,.2f),Scale=BallastScale,Breaks=false};
            for(int i=0;i<TubeCount;i++)w[i+2]=new HumBossWeakPoint{Id="tube-"+i,Joint=NativeHumBossLook.Tube(i),Half=new Vector3(.09f,.09f,.32f),Scale=1,Breaks=true,Share=TubeShare};
            return w;
        }
        /// <summary>Triangle budgets per tier (full, reduced, low; doc and tools/gear/hum_boss.py).</summary>
        public static readonly int[] Budget={40000,20000,8000};

        /// <summary>The intro's five shots on the boss's own time (doc section 3 "It has heard you"), in the root's frame at its end pose: the far wall
        /// it comes through lies <see cref="NativeHumArena.IntroWallBehind"/> m behind it. Timed on <see cref="HumBossBeats"/>.</summary>
        public static readonly BossIntroShot[] Shots={
            // 0-2.2: wide at the room's edge, eye height, a very slow push down the empty hall: nothing there. The far rows go out, nearer and nearer.
            new BossIntroShot{From=0,To=2.2f,EyeA=new Vector3(-.5f,1.62f,18.2f),EyeB=new Vector3(-.4f,1.60f,16.2f),LookA=new Vector3(-.2f,1.3f,-7),LookB=new Vector3(-.2f,1.28f,-7),FovA=56,FovB=54,Ease=1},
            // 2.2-4.2: close on the wall under the one failing lamp: the paper bulges, four long fingers push through, it tears open onto black.
            new BossIntroShot{From=2.2f,To=4.2f,EyeA=new Vector3(2.5f,1.05f,-4.1f),EyeB=new Vector3(2.15f,1.12f,-4.85f),LookA=new Vector3(.8f,1.62f,-6.8f),LookB=new Vector3(.85f,1.64f,-6.7f),FovA=46,FovB=40,Ease=1},
            // 4.2-6.2: long lens, side on, its whole height: it walks out through the wall in the dark; the tubes strike up its spine one at a time.
            new BossIntroShot{From=4.2f,To=6.2f,EyeA=new Vector3(15.5f,1.55f,-3.75f),EyeB=new Vector3(15.5f,1.55f,-3.35f),LookA=new Vector3(0,1.55f,-6.3f),LookB=new Vector3(0,1.55f,-3.0f),FovA=19,FovB=19,Ease=1},
            // 6.2-8.0: a slow push on the head from below, as it comes on: it flickers and catches, turns to look, the diffuser splits. The card rises at 6.43.
            new BossIntroShot{From=6.2f,To=8f,EyeA=new Vector3(-.75f,1.5f,.9f),EyeB=new Vector3(-.5f,1.72f,3.45f),LookA=new Vector3(0,2.72f,-2.5f),LookB=new Vector3(0,2.86f,1.25f),FovA=46,FovB=36,Ease=1},
            // 8.0-9.07: whip back to the party's view; it straightens, head through the ceiling, then stoops; every light slams on at the drop.
            new BossIntroShot{From=8f,To=9.0678f,EyeA=new Vector3(-.5f,1.72f,3.45f),EyeB=new Vector3(1.1f,1.66f,10.5f),LookA=new Vector3(0,3.1f,1.25f),LookB=new Vector3(0,1.9f,.3f),FovA=36,FovB=62,Ease=2,Shake=.012f}};
    }

    /// <summary>One move's proposed numbers (doc section 3; Astra owns them).</summary>
    public struct HumBossMove
    {
        public string Id,Title;public int Phase;
        public float Wind,Strike,Follow,Recover,Cooldown,Damage,Reach,Radius,Arc,Width,Speed,Grab,PerSecond;
        public float Duration=>Wind+Strike+Follow+Recover;
    }

    /// <summary>A weak point: its id, the joint it rides (Core NativeHumBossLook), its half size round the joint, its damage multiplier, and whether it
    /// breaks (with its share of max health).</summary>
    public struct HumBossWeakPoint{public string Id;public int Joint;public Vector3 Half;public float Scale,Share;public bool Breaks;}
}
