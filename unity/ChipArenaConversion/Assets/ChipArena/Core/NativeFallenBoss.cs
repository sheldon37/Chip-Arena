using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL, a final boss (docs/EXPEDITION-BOSS-FINAL.md): the names the rest of the game reads, its card, and the design as data: the phases,
    /// the move table Astra's rules can read, the arena's measures. Presentation only until Astra binds the record: nothing here is read by a
    /// rule yet, and every number is a proposal Astra owns.
    /// </summary>
    public static class NativeFallenBoss
    {
        public const string Archetype="final-noctiel",Name="NOCTIEL";
        public static readonly BossIntroCard Card=new BossIntroCard{Eyebrow="LUX CECIDIT",Title="NOCTIEL",Line="THE LIGHT THAT FELL",Ink=0x000000,TopLine=0xffffff,Rule=0xd8d8d8,BarFrom=0x707070,BarTo=0xffffff,Letters=0xffffff,Glow=0xb8b8b8,SplitA=0xffffff,SplitB=0x202020,Shades=1};
        /// <summary>The four phases and the health each starts at.</summary>
        public const int Phases=4;
        public static readonly string[] PhaseNames={"GRACE","WRATH","RUIN","LUX CECIDIT"};
        public static readonly float[] PhaseFrom={1f,.7f,.4f,.15f};
        public static int Phase(float health){int p=1;for(int i=1;i<Phases;i++)if(health<=PhaseFrom[i])p=i+1;return p;}
        /// <summary>The body's scale over the player's skeleton: about 3.5 m tall.</summary>
        public const float Scale=1.95f;
        /// <summary>The arena: a disc this wide, wading slower past SlowFrom and pushed back at Edge.</summary>
        public const float ArenaRadius=80,SlowFrom=74,Edge=80;
        /// <summary>Lives a pilot enters with, and the rest of section 3's revive proposal.</summary>
        public const int Lives=5;public const float RespawnSeconds=3,ReviveSeconds=4,ReviveReach=3;
        /// <summary>The Hush: still within this radius for this long (the last phase shorter), and its damage.</summary>
        public const float HushRadius=3,HushSeconds=6,HushLast=4,HushDamage=40;
        /// <summary>The enrage (s into the fight).</summary>
        public const float EnrageAt=720;
        /// <summary>Health multiplier over the shared boss baseline: 12% above The Deep End.</summary>
        public const float HealthScale=1.4f;

        /// <summary>The moves (doc section 8, seconds, damage before party scaling). Kills: takes a whole life. Clip: the pack clip the body plays through
        /// the strike, from ClipFrom at ClipRate (the move's Wind lands on the clip's hit).</summary>
        public static readonly FallenBossMove[] Moves={
            new FallenBossMove{Id="cleave",Title="Cleave",Phase=1,Wind=.45f,Strike=.18f,Follow=.25f,Recover=.3f,Cooldown=3,Damage=26,Reach=8,Arc=150,Clip="Sword-Attack-R1"},
            new FallenBossMove{Id="vow",Title="Vow",Phase=1,Wind=.8f,Strike=.5f,Follow=.3f,Recover=.5f,Cooldown=9,Damage=34,Reach=35,Width=3,Speed=70,Clip="2Hand-Sword-Run-Forward-Attack1"},
            new FallenBossMove{Id="severance",Title="Severance",Phase=1,Wind=.9f,Strike=.2f,Follow=.4f,Recover=.6f,Cooldown=11,Damage=30,Reach=160,Width=2.5f,Speed=34,Clip="2Hand-Sword-Attack5"},
            new FallenBossMove{Id="descent",Title="Descent",Phase=1,Wind=1.4f,Strike=.15f,Follow=.5f,Recover=.7f,Cooldown=14,Damage=38,Radius=9,Clip="2Hand-Sword-Air-Attack1"},
            new FallenBossMove{Id="rain",Title="Feather Rain",Phase=2,Wind=1.2f,Strike=2.4f,Follow=.2f,Recover=.6f,Cooldown=16,Damage=18,Width=2.5f,Clip="Armed-Activate-Left"},
            new FallenBossMove{Id="verdict",Title="Verdict",Phase=2,Wind=2.4f,Strike=.2f,Follow=.6f,Recover=.9f,Cooldown=22,Kills=true,Reach=160,Width=3,Clip="2Hand-Sword-Attack11"},
            new FallenBossMove{Id="choir",Title="Choir",Phase=2,Wind=1.1f,Strike=.3f,Follow=.3f,Recover=.5f,Cooldown=28,Clip="Armed-Activate-Left"},
            new FallenBossMove{Id="sweep",Title="Wing Sweep",Phase=3,Wind=1f,Strike=.5f,Follow=.3f,Recover=.5f,Cooldown=12,Damage=34,Radius=28,Clip="2Hand-Sword-Attack3"},
            new FallenBossMove{Id="eclipse",Title="Eclipse",Phase=3,Wind=3.2f,Strike=.5f,Follow=.8f,Recover=.6f,Cooldown=26,Kills=true,Radius=6,Clip="2Hand-Sword-Activate"},
            new FallenBossMove{Id="penance",Title="Penance",Phase=3,Wind=1f,Strike=.3f,Follow=.3f,Recover=.5f,Cooldown=26,Clip="2Hand-Sword-Attack11"},
            new FallenBossMove{Id="fall",Title="Fall of Light",Phase=4,Wind=1.6f,Strike=3.2f,Follow=.3f,Recover=.6f,Cooldown=20,Kills=true,Radius=8,Clip="2Hand-Sword-Activate"},
            new FallenBossMove{Id="last",Title="Last Verdict",Phase=4,Wind=2.8f,Strike=.2f,Follow=.6f,Recover=.9f,Cooldown=24,Kills=true,Reach=160,Width=3,Clip="2Hand-Sword-Attack11"}};
        public static FallenBossMove Move(string id){foreach(var m in Moves)if(m.Id==id)return m;return default;}
        /// <summary>The next phase's modifiers the chip screen reveals (doc section 10; Astra draws them by seed). The preview's three per phase.</summary>
        public static readonly FallenBossChip[] Chips={
            new FallenBossChip{Id="second-wing",Name="SECOND WING",Line="One more Severance wave"},new FallenBossChip{Id="no-shade",Name="NO SHADE",Line="The Hush takes 1.5 s less"},
            new FallenBossChip{Id="full-choir",Name="FULL CHOIR",Line="One more add each summon"},new FallenBossChip{Id="unforgiven",Name="UNFORGIVEN",Line="A revive takes 2 s longer"},
            new FallenBossChip{Id="haste",Name="HASTE",Line="Windups 12% shorter"},new FallenBossChip{Id="long-reach",Name="LONG REACH",Line="Cleave and Wing Sweep 20% wider"},
            new FallenBossChip{Id="heavy-hand",Name="HEAVY HAND",Line="Melee hits 15% harder"},new FallenBossChip{Id="black-water",Name="BLACK WATER",Line="Ripples fade faster, the floor goes darker"},
            new FallenBossChip{Id="no-mercy",Name="NO MERCY",Line="Verdicts come sooner"}};
    }
    /// <summary>Where the planted sword stands: beside its right hand, a little ahead, as high as the fist closes on it.</summary>
    public static class NativeFallenSongPlant
    {
        public const float GripY=1.72f,LiftBy=1.9f;
        public static Vector3 At(Vector3 boss,float yawDegrees){double a=yawDegrees*Math.PI/180;float s=(float)Math.Sin(a),c=(float)Math.Cos(a);return boss+new Vector3(c,0,-s)*.85f+new Vector3(s,0,c)*.72f;}
    }
    /// <summary>One move's proposed numbers (Astra owns them).</summary>
    public struct FallenBossMove
    {
        public string Id,Title,Clip;public int Phase;public bool Kills;
        public float Wind,Strike,Follow,Recover,Cooldown,Damage,Reach,Radius,Arc,Width,Speed;
        public float Duration=>Wind+Strike+Follow+Recover;
    }
    /// <summary>One modifier chip the boss shows at a phase change.</summary>
    public struct FallenBossChip{public string Id,Name,Line;}

    /// <summary>
    /// What the bank's parts are and when they show (tools/gear/fallen_boss.py names the same parts): each rides a bone of the player's skeleton or a chain segment the
    /// game turns, and shows in the phases of its mask (bit 0 is phase 1). The chains' pivots are the script's numbers, in the skeleton's rest (mesh space: x the
    /// body's right, y up, z forward).
    /// </summary>
    public static class NativeFallenBossLook
    {
        public struct Part{public string Name,Group;public byte Phases;public int Spin;}
        public struct Chain{public string Name,Parent;public Vector3 Pivot;public int Side,Index,Segment;public byte Kind;}
        public const byte KindWing=0,KindRobe=1,KindHalo=2,KindRing=3;
        public const int RobeStrips=7,RobeSegments=3,WingPairs=3,RingBlades=12;
        public const string Prefix="fb-";
        public static readonly Part[] Parts;public static readonly Chain[] Chains;
        private static readonly float[] RobeAngle={-22,22,-82,82,-140,140,180},RobeLength={.3f,.3f,.32f};
        private const float RobeZ=.955f;
        private static readonly float[][] WingRoot={new[]{.05f,-.1f,1.42f},new[]{.055f,-.11f,1.33f},new[]{.05f,-.1f,1.24f}},WingWay={new[]{1f,-.25f,.62f},new[]{1f,-.3f,.14f},new[]{1f,-.3f,-.34f}},WingSeg={new[]{.34f,.44f,.48f},new[]{.3f,.38f,.42f},new[]{.26f,.32f,.36f}};
        private static readonly float[] WingBend={0,-.42f,-.9f};
        // The design frame (X right, Y forward, Z up) in mesh space.
        private static Vector3 Mesh(float x,float y,float z)=>new Vector3(x,z,y);
        public static Vector3 HaloPivot=Mesh(0,-.14f,1.72f),RingPivot=Mesh(0,-.2f,1.38f);
        static NativeFallenBossLook()
        {
            var p=new List<Part>();var c=new List<Chain>();
            void P(string name,string group,int phases)=>p.Add(new Part{Name=name,Group=group,Phases=(byte)phases});
            foreach(var b in new[]{"upperarm_r","lowerarm_r","upperarm_l","lowerarm_l","thigh_r","calf_r","thigh_l","calf_l","spine_02","spine_05","neck_01"}){P("under-"+b,b,7);P("core-"+b,b,8);}
            P("cuirass","spine_05",3);P("cuirass-broken","spine_05",12);P("glass","spine_05",12);P("heart","spine_05",12);P("cracks-chest","spine_05",2);P("abdomen","spine_02",7);P("faulds","pelvis",15);
            P("cowl","head",7);P("head-light","head",8);P("mask","head",3);P("mask-eyes","head",3);P("fan","spine_05",3);P("mask-split","head",4);P("split-light","head",4);
            c.Add(new Chain{Name="halo",Parent="head",Pivot=HaloPivot,Kind=KindHalo});P("halo","halo",3);for(int i=0;i<3;i++)P("halo-shard-"+i,"halo",4);
            foreach(var s in new[]{"r","l"}){P("pauldron-"+s,"upperarm_"+s,15);P("rerebrace-"+s,"upperarm_"+s,7);P("vambrace-"+s,"lowerarm_"+s,7);P("gauntlet-"+s,"hand_"+s,15);P("cuisse-"+s,"thigh_"+s,7);P("greave-"+s,"calf_"+s,7);P("sabaton-"+s,"foot_"+s,15);}
            for(int i=0;i<RobeStrips;i++)
            {
                float a=RobeAngle[i]*(float)Math.PI/180;Vector3 o=new Vector3((float)Math.Sin(a)*.14f,.03f+(float)Math.Cos(a)*.14f*.8f,RobeZ),outward=new Vector3((float)Math.Sin(a),(float)Math.Cos(a),0);
                for(int seg=0;seg<RobeSegments;seg++)
                {
                    string g="robe"+i+"-"+seg;c.Add(new Chain{Name=g,Parent=seg==0?"pelvis":"robe"+i+"-"+(seg-1),Pivot=Mesh(o.X,o.Y,o.Z),Kind=KindRobe,Index=i,Segment=seg});
                    P(g,g,seg==0?7:seg==1?3:1);if(seg==RobeSegments-1)P(g+"-burnt",g,2);
                    o+=new Vector3(0,0,-RobeLength[seg])+outward*.03f;
                }
            }
            for(int w=0;w<WingPairs;w++)for(int side=1;side>=-1;side-=2)
            {
                string tag=side>0?"r":"l";var q=new Vector3(WingRoot[w][0]*side,WingRoot[w][1],WingRoot[w][2]);var way=Vector3.Normalize(new Vector3(WingWay[w][0]*side,WingWay[w][1],WingWay[w][2]));
                for(int seg=0;seg<3;seg++)
                {
                    string g="wing"+w+tag+"-"+seg;c.Add(new Chain{Name=g,Parent=seg==0?"spine_05":"wing"+w+tag+"-"+(seg-1),Pivot=Mesh(q.X,q.Y,q.Z),Kind=KindWing,Side=side,Index=w,Segment=seg});
                    P(g,g,w==2?1:3);if(w==2)P(g+"-shed",g,2);P(g+"-bone",g,4);
                    var d=Vector3.Normalize(way+new Vector3(0,0,WingBend[seg]*.6f));q+=d*WingSeg[w][seg];
                }
            }
            for(int i=0;i<RingBlades;i++)c.Add(new Chain{Name="blade"+i,Parent="spine_05",Pivot=RingPivot,Kind=KindRing,Index=i});
            for(int i=0;i<RingBlades;i++)p.Add(new Part{Name="ring-blade",Group="blade"+i,Phases=8});
            P("sword-blade","hand_r",3);P("sword-blade-dark","hand_r",4);P("sword-blade-light","hand_r",8);P("sword-hilt","hand_r",15);P("sword-eye","hand_r",12);P("sword-words","hand_r",3);P("sword-veins-1","hand_r",1);P("sword-veins-2","hand_r",2);P("sword-flakes","hand_r",8);
            Parts=p.ToArray();Chains=c.ToArray();
        }
    }

    /// <summary>
    /// Lux Cecidit on the boss's clock (docs/EXPEDITION-BOSS-FINAL.md section 6): the map's sections and the tempo, and the level, the bass and the slams measured from the
    /// file (tools/fallen_song_map.py, Resources/FallenBossMusic.json). What the floor and the fight's timing read. Pure.
    /// </summary>
    public sealed class NativeFallenSong
    {
        public const string Id="music/lux-cecidit";
        /// <summary>The sections (clip s): the cutscene, each phase's loop, the breakdown under the chip screens, the death.</summary>
        public const float IntroFrom=4f,Drop=54.81f,P2=82.66f,Break=121.66f,P3=145.81f,P4=165.51f,DeathAt=225f,End=263.2f,Bpm=152;
        public static readonly float Beat=60f/Bpm,Bar=240f/Bpm;
        public float[] Level=Array.Empty<float>(),Bass=Array.Empty<float>(),Slams=Array.Empty<float>();public float Rate=8;
        /// <summary>The bars elapsed since the drop at clip time `t` (fraction kept): the floor's pulse is its fractional part in beats.</summary>
        public static float BeatsAt(float t)=>(t-Drop)/Beat;
        private static float Lerp(float[] a,float t,float rate)
        {
            if(a.Length==0)return 0;float x=Math.Max(0,t*rate);int i=(int)x;if(i>=a.Length-1)return a[a.Length-1];float u=x-i;return a[i]+(a[i+1]-a[i])*u;
        }
        public float LevelAt(float t)=>Lerp(Level,t,Rate);
        public float BassAt(float t)=>Lerp(Bass,t,Rate);
        /// <summary>The newest slam at or before `t` (clip s), NaN for none: a ring runs out from it.</summary>
        public float LastSlam(float t){float best=float.NaN;foreach(float s in Slams){if(s>t)break;best=s;}return best;}
        /// <summary>A section's loop for each phase (from, to, clip s).</summary>
        public static void Loop(int phase,out float from,out float to)
        {
            if(phase<=1){from=Drop;to=P2;}else if(phase==2){from=P2;to=Break;}else if(phase==3){from=P3;to=P4;}else{from=P4;to=DeathAt;}
        }
    }
}
