using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One move's proposed timeline and shape (docs/EXPEDITION-BOSS-BACKROOMS.md section 4; Astra owns the numbers). <c>Anticipation</c> is the
    /// telegraph, the hit lands at actionAt + Anticipation. <c>Radius</c> a circle's, <c>Reach</c> a line's or beam's length, <c>Width</c> a
    /// lane's, <c>Arc</c> a sweep's degrees, <c>Speed</c> the water's or the body's m/s. Damage in THE 6:47's units, before party scaling.
    /// </summary>
    public struct DeepEndBossMove
    {
        public string Id,Title;public int Phase;public float Anticipation,Strike,Follow,Recover,Cooldown,Damage,Tick,Radius,Reach,Width,Arc,Speed;
        public float Duration=>Anticipation+Strike+Follow+Recover;
    }

    /// <summary>
    /// THE DEEP END, the Poolrooms' boss (docs/EXPEDITION-BOSS-BACKROOMS.md section 4): the names the rest of the game reads, the moves table Astra's rules read, its weak
    /// points and hit volumes as data, the intro's shots and card. Presentation only until Astra binds the record.
    /// </summary>
    public static class NativeDeepEndBoss
    {
        public const string Archetype="poolrooms-deep-end",Name="THE DEEP END",Area=NativePoolroomsGenerator.AreaId;
        public const int Phases=3;public const float PhaseChangeSeconds=3.5f,PhaseTwoAt=.65f,PhaseThreeAt=.3f;
        /// <summary>The Lull's stillness meter (a pilot within <see cref="StillRadius"/> m of one spot): seconds per phase; the height rule (over 1.6 m for 2 s).</summary>
        public const float StillRadius=3,StillSeconds=6,StillSecondsLast=4,HighRule=1.6f,HighSeconds=2;
        /// <summary>The tide over the deck (m) in each phase, and the most it stays immune under water at a time; the 6:00 flood.</summary>
        public const float ImmuneMost=6,EnrageAt=360;
        public const string PhaseOne="THE SHALLOW END",PhaseTwo="HIGH TIDE",PhaseThree="THE DEEP END";
        public static readonly DeepEndBossMove[] Moves={
            new DeepEndBossMove{Id="breach",Title="Breach",Phase=1,Anticipation=1.1f,Strike=.25f,Follow=.5f,Recover=.6f,Cooldown=5,Damage=28,Radius=3.2f},
            new DeepEndBossMove{Id="jet",Title="Hydro Jet",Phase=1,Anticipation=1.0f,Strike=2.2f,Follow=.3f,Recover=.8f,Cooldown=12,Damage=10,Tick=.25f,Reach=30,Width=2,Arc=110},
            new DeepEndBossMove{Id="flop",Title="Belly Flop",Phase=1,Anticipation=1.8f,Strike=.2f,Follow=.6f,Recover=1,Cooldown=14,Damage=30,Reach=24,Width=4},
            new DeepEndBossMove{Id="thrash",Title="Thrash",Phase=1,Anticipation=.4f,Strike=.3f,Follow=.2f,Recover=.4f,Cooldown=6,Damage=18,Radius=5},
            new DeepEndBossMove{Id="drain",Title="The Drain",Phase=1,Anticipation=1.4f,Strike=4,Follow=.3f,Recover=.8f,Cooldown=20,Damage=40,Radius=2.6f,Speed=7},
            new DeepEndBossMove{Id="lull",Title="The Lull",Phase=1,Anticipation=1.5f,Strike=.3f,Follow=1.5f,Recover=0,Cooldown=0,Damage=8,Tick=1,Radius=1.8f},
            new DeepEndBossMove{Id="seiche",Title="Seiche",Phase=2,Anticipation=1.4f,Strike=7f,Follow=0,Recover=.5f,Cooldown=16,Damage=18,Width=2.6f,Speed=11.4f},
            new DeepEndBossMove{Id="whistle",Title="Whistle",Phase=2,Anticipation=1,Strike=2,Follow=0,Recover=.5f,Cooldown=25,Damage=0,Radius=6},
            new DeepEndBossMove{Id="lap",Title="Last Lap",Phase=3,Anticipation=1.5f,Strike=6,Follow=.5f,Recover=1,Cooldown=24,Damage=24,Width=3.2f,Radius=19,Speed=12}};
        public static DeepEndBossMove Move(string id){foreach(var m in Moves)if(m.Id==id)return m;return default;}
        public static int Index(string id){for(int i=0;i<Moves.Length;i++)if(Moves[i].Id==id)return i;return -1;}
        /// <summary>Last Lap's circle shrinks from <see cref="DeepEndBossMove.Radius"/> to this (m).</summary>
        public const float LapInner=9;
        /// <summary>Seiche's wave (m tall) and the Drain's pull at the room's edge and at the pit (m/s).</summary>
        public const float SeicheHeight=.9f,DrainPullMin=5,DrainPullMax=9;

        // ---- weak points and breaks ----
        /// <summary>The throat's damage multiplier while the mouth is open; a lamp's share of max health to break it; lamps broken for the stagger, its seconds; a stranding Flop's seconds.</summary>
        public const float ThroatMultiplier=2,LampShare=.02f,StaggerSeconds=3,StrandSeconds=2.5f;public const int StaggerLamps=4;
        /// <summary>The hit volumes: one sphere per spine joint at <see cref="NativeDeepEndBossLook.Radius"/> (a chain of capsules between them), the throat a sphere of
        /// <see cref="ThroatRadius"/> at the mouth, each lamp a sphere of its own radius. Colliders follow the authority's root; these are the Core model both read.</summary>
        public const float ThroatRadius=.95f;
        public static int HitVolumes=>NativeDeepEndBossLook.Spine;

        // ---- the intro (doc section 4, the table): in the boss's own frame, the origin the abyss's centre on the deck's level, z the way it faces ----
        public static readonly BossIntroShot[] Shots=MakeShots();
        private static BossIntroShot[] MakeShots()
        {
            var b=DeepEndBossBeats.Fallback;
            return new[]{
                // Wide over perfectly still water, gliding: warm light, the whole room's scale, its round lights in the mirror.
                new BossIntroShot{From=0,To=b.LowShot,EyeA=new Vector3(-16.2f,2.5f,-16.6f),EyeB=new Vector3(-10.4f,1.45f,-12.8f),LookA=new Vector3(2,-.6f,6),LookB=new Vector3(3.2f,-.25f,5),FovA=60,FovB=56,Ease=1},
                // Low at the surface, at the top of the steps, toward where they go down into the dark.
                new BossIntroShot{From=b.LowShot,To=b.DownShot,EyeA=new Vector3(.3f,.12f,-11.6f),EyeB=new Vector3(.1f,-.02f,-9.1f),LookA=new Vector3(0,-1.25f,4),LookB=new Vector3(0,-1.1f,6),FovA=52,FovB=47,Ease=1},
                // Straight down over the abyss, slowly descending: the spiral of lights rising out of the black.
                new BossIntroShot{From=b.DownShot,To=b.BreakShot,EyeA=new Vector3(.06f,6.55f,.07f),EyeB=new Vector3(0,2.2f,.03f),LookA=new Vector3(0,-30,0),LookB=new Vector3(0,-30,0),FovA=64,FovB=56,Ease=1},
                // Low and wide, looking up: it breaks the surface and goes on rising.
                new BossIntroShot{From=b.BreakShot,To=b.MouthShot,EyeA=new Vector3(5.3f,.06f,10.4f),EyeB=new Vector3(6.7f,.34f,12.6f),LookA=new Vector3(.2f,1.0f,.4f),LookB=new Vector3(.5f,3.95f,.5f),FovA=62,FovB=68,Ease=0},
                // Close on the mouth, then pulled back on the wave.
                new BossIntroShot{From=b.MouthShot,To=b.WashShot,EyeA=new Vector3(.8f,3.1f,8.6f),EyeB=new Vector3(1.2f,1.9f,10.4f),LookA=new Vector3(.2f,4.9f,2.6f),LookB=new Vector3(.1f,4.5f,2.4f),FovA=38,FovB=46,Ease=1},
                new BossIntroShot{From=b.WashShot,To=b.Hold+.4f,EyeA=new Vector3(1.2f,1.9f,10.4f),EyeB=new Vector3(1.4f,.55f,16.4f),LookA=new Vector3(.1f,4.5f,2.4f),LookB=new Vector3(0,2.4f,0),FovA=46,FovB=60,Ease=2,Shake=.05f}};
        }
        public static readonly BossIntroCard Card=new BossIntroCard{Eyebrow="BOSS · THE POOLROOMS",Title="THE DEEP END",Line="THE WATER IS ALWAYS WARM",Ink=0x010608,TopLine=0xc4f4f4,Rule=0x9eeeee,BarFrom=0x1a9ea6,BarTo=0xf0ffff,Letters=0xf4ffff,Glow=0x6fe8e8,SplitA=0x40d8d8,SplitB=0xf8fff8};
    }

    /// <summary>
    /// The body's measures (Core mirror of tools/gear/deep_end_boss.py, held to it by Tests/DeepEndBossChecks): its joints along the spine, the radius along it, the twelve flank
    /// lamps, the mouth's frame, the rigid parts' joints and the bank's names. Pure.
    /// </summary>
    public static class NativeDeepEndBossLook
    {
        public const float Length=32;public const int Segments=30,Spine=Segments+1;public const float Seg=Length/Segments;
        // The rigid parts' joints, after the spine's.
        public const int FinR=Spine,FinL=Spine+1,RailR=Spine+2,RailL=Spine+3,Grate=Spine+4,Throat=Spine+5,BladeFirst=Spine+6,Blades=9,Count=BladeFirst+Blades;
        public const string Bank="ChipArenaPackBossDeepEnd";
        /// <summary>The bank's prop per tier: deepend-&lt;part&gt;-&lt;tier&gt;.</summary>
        public static string Prop(string part,int tier)=>"deepend-"+part+"-"+tier;
        public static readonly string[] Tiers={"0","1","2"};
        /// <summary>The mouth's centre in the head joint's frame (y its back, z the mouth).</summary>
        public static readonly Vector3 Mouth=new Vector3(0,-.16f,0);
        public const float MouthRadius=1.02f,ThroatDepth=.46f,GrateDepth=.34f,BladeDepth=.06f,BladeStep=.012f;
        public static readonly float[] LampS={6,10,14,18,22,26};public const int Lamps=12;
        /// <summary>The coping's angle round the body from the back (radians), the fins' root (s, th), the rails' root (s, th), as the model's.</summary>
        public const float CopingTheta=1.59f,FinS=4.6f,FinTheta=1.58f,RailS=.95f,RailTheta=1.7f;
        public const string Text3="3 FT",Text5="5 FT",Text9="9 FT",TextNo="NO DIVING",TextCrown="DEEP END";
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Sm(float a,float b,float x){float t=Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        /// <summary>The radius at s m behind the mouth (the model's `radius`).</summary>
        public static float Radius(float s)
        {
            if(s<1.4f){float u=Clamp01(1-s/1.4f);return 1.05f+.35f*(float)Math.Sqrt(Math.Max(0,1-u*u));}
            if(s<3.6f)return 1.40f+(1.14f-1.40f)*Sm(1.4f,3.6f,s);
            if(s<9f)return 1.14f+.06f*Sm(3.6f,9f,s);
            float k=Clamp01((s-9f)/(Length-9f));return .22f+(1.2f-.22f)*(float)Math.Pow(Math.Max(0,1-Math.Pow(k,1.7)),.85);
        }
        /// <summary>A point on the skin in the spine's frame at s: (x, y) with th from the back toward the right (the model's `surf`, without its small swellings), z 0 at s.</summary>
        public static Vector3 Skin(float s,float th)
        {
            float r=Radius(s),wx=s<21?1:1-.34f*Sm(21,31,s),wz=.8f+.2f*Sm(2.6f,6,s)+.12f*(1-Sm(0,1.6f,s));float zc=-.16f*(float)Math.Pow(Clamp01(1-s/3.6f),2);
            float sn=(float)Math.Sin(th),cs=(float)Math.Cos(th);const float e=2f/2.25f;float sx=(float)(Math.Sign(sn)*Math.Pow(Math.Abs(sn),e)),cz=(float)(Math.Sign(cs)*Math.Pow(Math.Abs(cs),e));
            float x=wx*r*sx,y=wz*r*cz;if(cs<0)y*=.92f;return new Vector3(x,zc+y,0);
        }
        /// <summary>A flank lamp: index 0..11 (even the right, odd the left, toward the tail by twos). Its s, side and radius (the bezel's).</summary>
        public static void Lamp(int i,out float s,out int side,out float radius){s=LampS[i/2];side=i%2==0?1:-1;radius=.34f*Math.Min(1,Radius(s))+.05f;}
        /// <summary>A joint coordinate (s / Seg) as the cubic B-spline's four bones and weights: how the skin follows the spine (Runtime builds the bone weights from it).</summary>
        public static void Weights(float q,out int b0,out float w0,out float w1,out float w2,out float w3)
        {
            int i=(int)Math.Floor(q);float f=q-i;if(i<0){i=0;f=0;}if(i>=Segments){i=Segments;f=0;}b0=i-1;
            float f2=f*f,f3=f2*f;w0=(1-3*f+3*f2-f3)/6;w1=(4-6*f2+3*f3)/6;w2=(1+3*f+3*f2-3*f3)/6;w3=f3/6;
        }
        // The stand-in without the bank (CI, other checkouts): a plain tapering loft, white above and turquoise below, drawn with the gear's toon.
        public static void Fallback(out Vector3[] pos,out Vector3[] nor,out Vector4[] col,out Vector2[] uv,out int[] tri)
        {
            const int rings=64,round=14;int n=(rings+1)*round;pos=new Vector3[n+1];nor=new Vector3[n+1];col=new Vector4[n+1];uv=new Vector2[n+1];tri=new int[rings*round*6+round*3];int t=0;
            for(int r=0;r<=rings;r++)for(int k=0;k<round;k++)
            {
                float s=Length*r/rings,th=BossMath.Tau*k/round;var p=Skin(s,th);int i=r*round+k;pos[i]=new Vector3(p.X,p.Y,-s);nor[i]=Vector3.Normalize(new Vector3((float)Math.Sin(th),(float)Math.Cos(th),0));
                bool belly=Math.Cos(th)<-.1;bool lane=Math.Abs(BossMath.WrapPi(th))<.1f;col[i]=belly?new Vector4(.66f,.89f,.88f,1):lane?new Vector4(.07f,.09f,.11f,1):new Vector4(.95f,.96f,.95f,1);uv[i]=new Vector2(s/Seg,1);
                if(r<rings){int a=i,b=r*round+(k+1)%round,c=b+round,d=a+round;tri[t++]=a;tri[t++]=b;tri[t++]=c;tri[t++]=a;tri[t++]=c;tri[t++]=d;}
            }
            pos[n]=new Vector3(0,-.16f,.02f);nor[n]=Vector3.UnitZ;col[n]=new Vector4(.04f,.05f,.06f,1);uv[n]=Vector2.Zero;for(int k=0;k<round;k++){tri[t++]=n;tri[t++]=(k+1)%round;tri[t++]=k;}
        }
    }

    /// <summary>The boss's moves on the fixed beats of the intro, in one table (doc section 4, section 5: until the owner's song arrives they stay these numbers; <see cref="NativeDeepEndBossBeats"/>
    /// binds them to a song's accents). Seconds of boss time from the start of the hold.</summary>
    public struct DeepEndBossBeats
    {
        public float Hold,NameAt,Ripple,LowShot,LightsOut0,LightsOut1,DownShot,BreakShot,Break,MouthShot,IrisOpen,Roar,WashShot,WaveAt;public bool Song;
        public static readonly DeepEndBossBeats Fallback=new DeepEndBossBeats{Hold=NativeBackroomsBossIntro.Hold,NameAt=NativeBackroomsBossIntro.NameAt,Ripple=.55f,LowShot=2.3f,LightsOut0=2.45f,LightsOut1=4.2f,DownShot=4.3f,BreakShot=5.9f,Break=5.96f,MouthShot=8f,IrisOpen=8.08f,Roar=8.3f,WashShot=8.62f,WaveAt=8.12f};
        /// <summary>Whether the beats are in order (a song's accents that can't place them are refused).</summary>
        public bool Valid=>Ripple<LowShot&&LowShot<LightsOut0&&LightsOut0<LightsOut1&&LightsOut1<=DownShot&&DownShot<BreakShot&&BreakShot<=Break&&Break<MouthShot&&MouthShot<IrisOpen&&IrisOpen<Roar&&Roar<WashShot&&WashShot<Hold;
        /// <summary>The beats placed on a song's accents, as StationBossBeats.FromSong does: the break on the accent nearest its fixed time. False when the song can't place them.</summary>
        public static bool FromSong(NativeZoneBossSong s,out DeepEndBossBeats b)
        {
            b=Fallback;if(s?.Accents==null||s.Accents.Length==0)return false;double best=double.MaxValue;float at=b.Break;
            foreach(var a in s.Accents){double d=Math.Abs(a.T-Fallback.Break);if(d<best){best=d;at=(float)a.T;}}
            if(best>.45f)return false;float shift=at-b.Break;b.Break=at;b.BreakShot+=shift;b.Song=true;return b.Valid;
        }
    }
    public static class NativeDeepEndBossBeats
    {
        private static DeepEndBossBeats bound;private static bool has;private static NativeZoneBossSong song;
        public static DeepEndBossBeats Current=>has?bound:DeepEndBossBeats.Fallback;
        public static bool UseSong(NativeZoneBossSong s)
        {
            if(s==null){has=false;song=null;return false;}if(ReferenceEquals(s,song))return has;
            song=s;if(DeepEndBossBeats.FromSong(s,out var b)){bound=b;has=true;return true;}has=false;return false;
        }
    }
}
