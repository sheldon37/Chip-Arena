using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One NOCTIEL cue at a moment: its key (NativeFallenBossCues), where (unless it plays flat) and how loud.</summary>
    public struct FallenBossCue{public byte Key;public float At,Gain;public Vector3 At3;public bool Flat;}
    /// <summary>What a live fight's cues read this frame (all replicated, plus the local pilot as the listener).</summary>
    public struct FallenCueFight
    {
        public Vector3 Boss,Listener;public bool HasListener;
        public string Action;public float ActionAt,HitAt,ActionYaw;public Vector3 From,To;public bool SecondWing;public int Seed;public V3[] Pools;
        /// <summary>The phase change's bar (-100 none) and when this client first saw it; the draft's start; the death; the fight's start (the enrage is 12:00 on).</summary>
        public float PhaseChangeAt,PhaseSeenAt,DraftAt,DeathAt,Started;
        /// <summary>The local pilot's stillness clock (Hush): when it last moved or was hit by it (-100 none), how long it may stand, and where.</summary>
        public float HushAt,HushSeconds;public Vector3 HushPlace;
    }
    /// <summary>
    /// NOCTIEL's sounds (docs/audio/sound-requests-noctiel.json, docs/EXPEDITION-BOSS-FINAL.md section 17): the `boss.noctiel.*`
    /// keys and when each plays. The owner generates the clips; until the batch is imported a key has no clips and is silent.
    /// The keys are in the bank contract (Core/NativeSoundBank.cs); 2D (<see cref="Flat"/>) keys play flat, the rest at their place.
    /// <see cref="Cutscene"/> and <see cref="Fight"/> list the cues whose moment falls in a window of the cutscene's clock or the
    /// fight's; Runtime NativeFallenBossAudio.cs adds what only a change in the replicated state shows (adds, lives, revives, the Hush
    /// opening, a hit on it, its glide, turns and footfalls) and NativeAudio plays them through the bank (no stand-in), so a key with
    /// no clips is silent. Pure and allocation free.
    /// </summary>
    public static class NativeFallenBossCues
    {
        public static readonly string[] Keys={
            "boss.noctiel.step","boss.noctiel.glide","boss.noctiel.wings.open","boss.noctiel.wings.beat","boss.noctiel.plate","boss.noctiel.hurt",
            "boss.noctiel.feather","boss.noctiel.ripple","boss.noctiel.gauntlet","boss.noctiel.veins","boss.noctiel.halo.crack","boss.noctiel.rise",
            "boss.noctiel.mask","boss.noctiel.sword.pull","boss.noctiel.slash","boss.noctiel.slash.part","boss.noctiel.slowdown","boss.noctiel.kneel",
            "boss.noctiel.chip","boss.noctiel.cleave","boss.noctiel.vow.draw","boss.noctiel.vow","boss.noctiel.severance.wind","boss.noctiel.severance",
            "boss.noctiel.wave","boss.noctiel.leap","boss.noctiel.descent.mark","boss.noctiel.land","boss.noctiel.rain.lines","boss.noctiel.rain",
            "boss.noctiel.verdict.plant","boss.noctiel.verdict.crack","boss.noctiel.verdict.split","boss.noctiel.choir","boss.noctiel.cantor","boss.noctiel.cantor.death",
            "boss.noctiel.sweep.shake","boss.noctiel.sweep","boss.noctiel.eclipse","boss.noctiel.eclipse.pool","boss.noctiel.penance","boss.noctiel.penitent",
            "boss.noctiel.penitent.latch","boss.noctiel.fall.mark","boss.noctiel.fall","boss.noctiel.throne","boss.noctiel.throne.fire","boss.noctiel.throne.break",
            "boss.noctiel.hush","boss.noctiel.hush.open","boss.noctiel.life.lost","boss.noctiel.return","boss.noctiel.revive","boss.noctiel.revive.done",
            "boss.noctiel.death","boss.noctiel.shatter","boss.noctiel.enrage"};
        /// <summary>When each key plays, in the same order (the doc's table reads from this).</summary>
        public static readonly string[] Briefs={
            "a footfall on the water (the feet the live motion plants)","it takes off into a glide to chase","its wings spread (the cutscene, a long charge, Feather Rain, Fall of Light)",
            "the wings thrown out on a strike","standing turns and small moves: its plate shifting","a pilot's hit lands on it",
            "the cutscene's one feather touches the water (0:04-0:15)","a slow ring across the whole water (the feather, each pair of wings opening)","the close-up: the gauntlet on the hilt (0:28)",
            "black veins crawl up the blade","the crack runs through the halo","it rises from its knee (0:39)",
            "the blank mask lifts toward the lens (0:48)","it pulls the sword out of the water","the Slash cuts the screen (the opening on the drop, every phase change)",
            "the two halves of the frame slide apart","time slows hard as it swings at the camera","it drops to one knee at a threshold",
            "each of its three modifier chips turns over on the beat","each of Cleave's three cuts","Vow's tell: the water ahead parts in a line",
            "Vow's thrust crosses its lane in a blink","Severance's tell: the blade held wide","the flat swing throws the crescent across the map (and the second wing)",
            "the crescent passing close to a pilot","Descent: it leaps into the dark","Descent's tell: the white ring closing on its pilot",
            "Descent lands on the hit","Feather Rain's tell: thin lines of light on the water","a steel feather falls into its lane",
            "Verdict / Last Verdict: it plants the sword","the OHK tell: the crack brightens across the whole map (2.4 s; 2.8 s for Last Verdict)","the map splits along the crack",
            "Choir: it lifts its off hand and the Cantors come","a Cantor sings its beam at a pilot","a Cantor is killed",
            "Wing Sweep's tell: the blade wings spread and shake","the full-circle Wing Sweep","the OHK tell: the dark rolls in from the edge (3.2 s)",
            "a pool of light opens on the water","Penance: it pours from the blade","a Penitent rises from the water by a pilot",
            "a Penitent latches on","Fall of Light's tell: a bright square on the water, one bar ahead","a column of light falls",
            "a Throne hangs and turns","a Throne shoots down at a pilot","a Throne is brought down",
            "the Hush warning: the water stills and brightens under a pilot who stands still","the water opens under a pilot who stayed still","a pilot loses a feather (a life)",
            "a pilot falls back in from the dark","a teammate starts giving a feather (the revive hold)","the feather is given and the pilot stands",
            "NOCTIEL falls: its plate cracks and the light goes out","the hollow shell shatters into the water (the song's hit at 4:08)","The Light Goes Out (the 12:00 enrage): white, then black"};
        public const byte Step=0,Glide=1,WingsOpen=2,WingsBeat=3,Plate=4,Hurt=5,Feather=6,Ripple=7,Gauntlet=8,Veins=9,HaloCrack=10,Rise=11,Mask=12,SwordPull=13,Slash=14,SlashPart=15,Slowdown=16,Kneel=17,Chip=18,Cleave=19,VowDraw=20,Vow=21,SeveranceWind=22,Severance=23,Wave=24,Leap=25,DescentMark=26,Land=27,RainLines=28,Rain=29,VerdictPlant=30,VerdictCrack=31,VerdictSplit=32,Choir=33,Cantor=34,CantorDeath=35,SweepShake=36,Sweep=37,Eclipse=38,EclipsePool=39,Penance=40,Penitent=41,PenitentLatch=42,FallMark=43,Fall=44,Throne=45,ThroneFire=46,ThroneBreak=47,Hush=48,HushOpen=49,LifeLost=50,Return=51,Revive=52,ReviveDone=53,Death=54,Shatter=55,Enrage=56;
        /// <summary>The 2D keys: the cutscene, the Slash and chip screen, the map-wide tells, the Hush, a pilot's own lives, the end.</summary>
        public static bool Flat(int key)=>key==6||key==7||key==8||key==9||key==10||key==11||key==12||key==13||key==14||key==15||key==16||key==18||key==28||key==31||key==32||key==38||key==48||key==50||key==52||key==53||key==54||key==55||key==56;
        // ---------------- the moments ----------------
        public const int QueueSize=64;private static readonly FallenBossCue[] queue=new FallenBossCue[QueueSize];private static int queued;
        /// <summary>A cue for NativeAudio to play this frame (dropped when the frame's queue is full).</summary>
        public static void Queue(byte key,float at,Vector3 place,float gain=1,bool flat=false){if(queued<QueueSize)queue[queued++]=new FallenBossCue{Key=key,At=at,At3=place,Gain=gain,Flat=flat||Flat(key)};}
        public static int Drain(FallenBossCue[] into){int k=Math.Min(queued,into.Length);Array.Copy(queue,into,k);queued=0;return k;}
        private static FallenBossCue[] into;private static int n;private static float from,to;private static bool allFlat;
        private static void Add(byte key,float at,Vector3 place,float gain=1){if(!(at>from&&at<=to)||into==null||n>=into.Length)return;into[n++]=new FallenBossCue{Key=key,At=at,At3=place,Gain=gain,Flat=allFlat||Flat(key)};}
        private static void Begin(FallenBossCue[] buffer,float fromTime,float toTime,bool flat){into=buffer;n=0;from=fromTime;to=toTime;allFlat=flat;}

        /// <summary>
        /// The cutscene "The light fell" on its own clock (the intro hold's elapsed seconds, or the DEV preview's): the feather, the
        /// gauntlet, the veins, the halo, the rise and the wings, the mask, the sword drawn from the water, the slow swing at the lens
        /// and the opening Slash. All flat: the camera, not the pilot, is where the cutscene is heard.
        /// </summary>
        public static int Cutscene(float fromTime,float toTime,FallenBossCue[] buffer)
        {
            Begin(buffer,fromTime,toTime,true);if(!(to>from))return 0;var o=Vector3.Zero;
            Add(Feather,NativeFallenBossPreview.FeatherLand,o);Add(Ripple,NativeFallenBossPreview.FeatherLand+.05f,o,.8f);
            Add(Gauntlet,NativeFallenBossPreview.GauntletAt,o);Add(Veins,NativeFallenBossPreview.BladeAt,o);Add(HaloCrack,NativeFallenBossPreview.HaloAt,o);Add(Rise,NativeFallenBossPreview.RiseAt,o);
            for(int w=0;w<3;w++){Add(WingsOpen,NativeFallenBossPreview.WingAt[w],o,.8f+.1f*w);Add(Ripple,NativeFallenBossPreview.WingAt[w]+.4f,o,.6f);}
            Add(Mask,NativeFallenBossPreview.MaskAt,o);Add(SwordPull,NativeFallenIntroMotion.PullTime,o);Add(Slowdown,NativeFallenBossPreview.CutAt,o);
            float end=NativeFallenPilots.CutsceneEnd;Add(Slash,end-.5f,o);Add(SlashPart,end,o);
            return n;
        }

        /// <summary>
        /// A live fight's cues on the arena clock: each move's tell and strike on the authority's times (Cleave's three cuts, Severance's
        /// waves and where one passes the listener, Descent's ring, leap and landing, Feather Rain's lines and blades on the lanes nearest
        /// the listener, the Verdict's crack and split, the Eclipse and its pools, Fall of Light's marks and columns nearest the
        /// listener, the summons), the phase change's Slash, the chips turning over, the death, the enrage, and the local pilot's Hush
        /// warning 1.5 s before the water opens.
        /// </summary>
        public static int Fight(in FallenCueFight v,float fromTime,float toTime,FallenBossCue[] buffer)
        {
            Begin(buffer,fromTime,toTime,false);if(!(to>from))return 0;var b=v.From;
            if(v.Action!=null&&v.Action!="arrive")
            {
                float A=v.ActionAt,H=v.HitAt;
                switch(v.Action)
                {
                    case "cleave":for(int i=0;i<3;i++)Add(Cleave,H+.3f*i,v.Boss,i==2?1.1f:1);Add(WingsBeat,H+.6f,v.Boss,.7f);break;
                    case "vow":Add(VowDraw,A,b);Add(Vow,H,v.HasListener?Nearest(b,v.To,v.Listener):b);Add(WingsBeat,H,b,.6f);break;
                    case "severance":
                        Add(SeveranceWind,A,b);
                        for(int i=0;i<(v.SecondWing?2:1);i++)
                        {
                            float at=H+.6f*i;Add(Severance,at,b,i==0?1:.85f);if(!v.HasListener)continue;
                            var d=new Vector2(v.Listener.X-b.X,v.Listener.Z-b.Z);float r=d.Length();
                            if(r>4&&Vector2.Dot(d/r,NativeFallenRules.Dir(v.ActionYaw))>=.5f&&(r-3)/34<=5)Add(Wave,at+(r-3)/34,v.Listener);
                        }
                        break;
                    case "descent":Add(DescentMark,A,v.To);Add(Leap,NativeFallenLiveMotion.Takeoff(A,H),b);Add(Land,H,v.To,1.1f);Add(Ripple,H+.1f,v.To,.6f);break;
                    case "rain":Add(RainLines,A,b);Add(WingsOpen,A,b);for(int i=0;i<6;i++)Add(Rain,H+.38f*i,RainAt(v,i*3%8),.9f);break;
                    case "verdict":case "last":Add(VerdictCrack,A,b);Add(VerdictPlant,A+.15f,b);Add(VerdictSplit,H,b);break;
                    case "choir":case "thrones":Add(Choir,A,b);break;
                    case "penance":Add(Penance,A,b);break;
                    case "sweep":Add(SweepShake,A,b);Add(Sweep,H,b,1.1f);break;
                    case "eclipse":Add(Eclipse,A,b);Add(WingsOpen,A,b,.8f);if(v.Pools!=null)for(int i=0;i<v.Pools.Length&&i<4;i++)Add(EclipsePool,A+.5f+.15f*i,new Vector3(v.Pools[i].x,0,v.Pools[i].z),.8f);break;
                    case "fall":Add(WingsOpen,A,b,.8f);for(int i=0;i<4;i++){var c=ColumnNear(v,i);Add(FallMark,A+.1f*i,c,.8f);Add(Fall,H+.06f*i,c);}break;
                }
            }
            if(v.PhaseChangeAt>=0){float s=Math.Max(v.PhaseSeenAt,v.PhaseChangeAt-1.7f);Add(Slowdown,s,v.Boss);Add(Slash,s,v.Boss);Add(SlashPart,Math.Max(s,v.PhaseChangeAt-1.2f),v.Boss);}
            if(v.DraftAt>=0)for(int i=0;i<3;i++)Add(Chip,v.DraftAt+1.2f+i*2*NativeFallenSong.Beat,v.Boss);
            if(v.DeathAt>=0){Add(Death,v.DeathAt+.2f,v.Boss);Add(VerdictPlant,v.DeathAt+4.6f,v.Boss,.8f);Add(Shatter,v.DeathAt+NativeFallenBossPreview.DeathHit,v.Boss);}
            if(v.Started>=0)Add(Enrage,v.Started+720,v.Boss);
            if(v.HushAt>=0&&v.HushSeconds>1.5f)Add(Hush,v.HushAt+v.HushSeconds-1.5f,v.HushPlace);
            return n;
        }
        // The point of the lane from a to c nearest p (Vow's thrust heard where it passes the listener).
        private static Vector3 Nearest(Vector3 a,Vector3 c,Vector3 p){var d=c-a;float l=d.LengthSquared();float t=l>1e-6f?Math.Clamp(Vector3.Dot(p-a,d)/l,0,1):0;return a+d*t;}
        // Feather Rain's lane (the authority's: NativeFallenAuthority.FallenAttack), at its point nearest the listener.
        public static Vector3 RainAt(in FallenCueFight v,int lane)
        {
            float angle=v.Seed*37+lane*45+(lane%2)*19;var dd=NativeFallenRules.Dir(angle);var c=new Vector2(v.From.X,v.From.Z)*.5f+new Vector2(dd.Y,-dd.X)*(lane-3.5f)*7;
            if(v.HasListener){var q=new Vector2(v.Listener.X,v.Listener.Z);c+=dd*Math.Clamp(Vector2.Dot(q-c,dd),-80,80);}return new Vector3(c.X,0,c.Y);
        }
        // Fall of Light's k-th nearest falling column to the listener (the authority's cells: NativeFallenRules.Column), its centre.
        public static Vector3 ColumnNear(in FallenCueFight v,int k)
        {
            var q=v.HasListener?new Vector2(v.Listener.X,v.Listener.Z):Vector2.Zero;float last=-1;Vector3 pick=v.Boss;
            for(int r=0;r<=k;r++)
            {
                float best=float.MaxValue;var at=pick;
                for(int x=-10;x<=10;x++)for(int z=-10;z<=10;z++)
                {
                    if(!NativeFallenRules.Column(x,z,v.Seed))continue;float d=Vector2.DistanceSquared(q,new Vector2(x*8+4,z*8+4))+(x*31+z)*1e-6f;
                    if(d>last&&d<best){best=d;at=new Vector3(x*8+4,0,z*8+4);}
                }
                if(best==float.MaxValue)return pick;last=best;pick=at;
            }
            return pick;
        }
    }
}
