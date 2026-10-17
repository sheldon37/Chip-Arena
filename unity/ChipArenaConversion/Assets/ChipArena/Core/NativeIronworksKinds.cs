using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What the presentation reads of an Ironworks add beyond today's replicated minion record (docs/EXPEDITION-CITY-IRONWORKS.md
    /// 10.4: the fields the rules will replicate; Astra fills them, the presentation derives everything else). Times are the
    /// arena clock (NaN: not happening); indices -1 when unset. <see cref="None"/> is every add's view until the rules replicate
    /// them (Runtime/NativeIronworksKindsPresentation.cs IronworksViewOf is the one-line binding), and what every Siren kind has.
    /// </summary>
    public struct IronworksView
    {
        /// <summary>Stoker: the pilot (seat id) its hook caught (-1: none or a miss) and when its pull began (the hook raised:
        /// the throw is <see cref="NativeIronworksKinds.PullWindup"/> later).</summary>
        public int pullTarget;public float pullAt;
        /// <summary>Slagback: when its crust shed (NaN: crusted).</summary>
        public float shedAt;
        /// <summary>Ladleman: when its pour began (the ladle raised; the pour is <see cref="NativeIronworksKinds.PourWindup"/> later)
        /// and the pour's yaw (deg, Unity, as the add's yaw); when its ladle spilled over it (NaN: intact).</summary>
        public float pourAt,pourYaw,spilledAt;
        /// <summary>Rigger: the hang point it hangs from (the area's entry index; -1 on the ground); when it locked a pilot (its
        /// hook drop: the landing is <see cref="NativeIronworksKinds.DropLand"/> later) and the locked landing point (x, z); when it
        /// threw its hook up to climb and the hang point it climbs to; when its chain was cut or it was knocked off (NaN: not).</summary>
        public int hang;public float lockAt,lockX,lockZ,climbAt;public int climbTo;public float chainCutAt;
        public static readonly IronworksView None=new IronworksView{pullTarget=-1,pullAt=float.NaN,shedAt=float.NaN,pourAt=float.NaN,pourYaw=0,spilledAt=float.NaN,hang=-1,lockAt=float.NaN,lockX=0,lockZ=0,climbAt=float.NaN,climbTo=-1,chainCutAt=float.NaN};
    }

    /// <summary>
    /// The City Ironworks' five enemies as the presentation draws them (owner 2026-10-16: "Go with your picks, add the rigger as
    /// well"; docs/EXPEDITION-CITY-IRONWORKS.md sections 6 and 10): the Cinderling (swarm), Stoker (puller), Slagback (armoured
    /// heavy), Ladleman (elite) and Rigger (ambusher). Presentation only: their rows here are the zone contract's proposals for
    /// the DEV preview (Astra owns the rules, stats, AI and replication; PveContent has none of them yet), registered after the
    /// Siren's kinds (Core/NativeHordeZones) so a host that sends their ids draws them; their models (NativeIronworksModels),
    /// motion (NativeIronworksMotion) and effects (NativeIronworksFx) are their own files. Every telegraph time below is the
    /// doc's, derived on each peer from a replicated time (LastAttack, or the view's fields), so host, guests and the killcam
    /// show the same frame. Also the zone's one tint: the brute's seams cream (0xFFD27A) while this instance is loaded.
    /// </summary>
    public static class NativeIronworksKinds
    {
        public const string Map="expedition-ironworks";
        // ---- the five, in the zone's order (local index) ----
        public const int Cinderling=0,Stoker=1,Slagback=2,Ladleman=3,Rigger=4,KindCount=5;
        public static readonly string[] Ids={"cinderling","stoker","slagback","ladleman","rigger"};
        // ---- tells (sRGB): never the environment's plain orange (3.2: white-hot, cyan, cream, cold white) ----
        public const int WhiteHot=0xfff1d8,Cyan=0x5ce8ff,Cream=0xffe9c2,ColdWhite=0xe4eeff,Molten=0xffb04a,SlagCrack=0x8a3014,Fissure=0xffd9a0,LadleVein=0x2f8a9a;
        /// <summary>The brute's seams in this zone (owner decision 14) and their default elsewhere (NativeHordeKinds.Seam).</summary>
        public const int BruteSeam=0xffd27a,BruteSeamDefault=0xff6a2c;
        /// <summary>Whether the City Ironworks instance is the loaded map (set by the presentation each frame from the arena's map id:
        /// <see cref="ZoneOf"/>); only the brute's seams read it.</summary>
        public static bool Zone;
        public static bool ZoneOf(string mapId)=>string.Equals(mapId,Map,StringComparison.Ordinal);

        /// <summary>The five as zone kinds (the zone contract's 10.1 rows and capsules; their scale, weight, glow and takes).</summary>
        public static readonly HordeZoneKind[] Kinds={
            // the runner model at .93 in the add capsule; packs sprint on the runner's takes
            new HordeZoneKind{Id="cinderling",Name="Cinderling",Zone=Map,Body=NativeHordeKinds.Runner,Looks=2,Scale=.93f,Mass=.8f,Seam=WhiteHot,SeamClass=0,Attack=NativeHordeKinds.Attack.Lunge,Tempo=1.04f,
                Proposed=Row("cinderling","lunge",30,4,2.8f,5.0f,1.8f,2.4f,.38f,.9f,1.1f,50,15,.35f,.4f,.45f,0),
                Walks=new[]{"sprint-1","sprint-3"},Runs=new[]{"sprint-3","sprint-4"},Idles=new[]{"idle-3","shamble-2"},Wanders=new[]{"walk-4"}},
            // a shambler body at 1.07 in the heat suit; the proposed capsule is the add capsule at its size (.45 x 1.82)
            new HordeZoneKind{Id="stoker",Name="Stoker",Zone=Map,Body=NativeHordeKinds.Shambler,Looks=2,Scale=1.07f,Radius=.45f,Height=1.82f,Mass=1.15f,Seam=Cyan,SeamClass=1,Attack=NativeHordeKinds.Attack.Swipe,Tempo=.94f,
                Proposed=Row("stoker","sweep",90,8,1.4f,2.8f,2.6f,2.2f,.5f,1.4f,.3f,80,40,.35f,.45f,.35f,0),
                Walks=new[]{"walk-6"},Runs=new[]{"chase-2"},Idles=new[]{"idle-3"},Wanders=new[]{"walk-4"}},
            // the brute crusted in slag, in the add capsule
            new HordeZoneKind{Id="slagback",Name="Slagback",Zone=Map,Body=NativeHordeKinds.Brute,Looks=2,Scale=1,Mass=3.2f,Seam=SlagCrack,SeamClass=0,Attack=NativeHordeKinds.Attack.Smash,Tempo=.84f,
                Proposed=Row("slagback","smash",320,16,1.2f,2.2f,2.1f,1.7f,.55f,1.9f,.3f,450,90,.45f,.5f,.2f,NativeHordeKinds.SmashRadius),
                Walks=new[]{"walk-4"},Runs=new[]{"chase-5"},Idles=new[]{"idle-3"},Wanders=new[]{"walk-4"}},
            // a big brute at 1.24 in the contract's .62 x 2.1 capsule
            new HordeZoneKind{Id="ladleman",Name="Ladleman",Zone=Map,Body=NativeHordeKinds.Brute,Looks=1,Scale=1.24f,Radius=.62f,Height=2.1f,Mass=4.2f,Seam=LadleVein,SeamClass=1,Attack=NativeHordeKinds.Attack.Smash,Tempo=.7f,
                Proposed=Row("ladleman","swing",700,18,.9f,1.4f,2.4f,2.0f,.6f,2.4f,.15f,600,220,.5f,.55f,.2f,0),
                Walks=new[]{"walk-4"},Runs=new[]{"walk-4"},Idles=new[]{"idle-3"},Wanders=new[]{"walk-4"}},
            // the shambler body in the shambler's capsule (the contract's; drawn at 1, not the doc's 1.04, so its hat stays inside)
            new HordeZoneKind{Id="rigger",Name="Rigger",Zone=Map,Body=NativeHordeKinds.Shambler,Looks=2,Scale=1,Mass=.9f,Seam=ColdWhite,SeamClass=2,Attack=NativeHordeKinds.Attack.Swipe,Tempo=1,
                Proposed=Row("rigger","swing",120,9,1.5f,3.4f,3.2f,3.0f,.6f,1.6f,.2f,260,45,.35f,.45f,.35f,0),
                Walks=new[]{"walk-6","chase-1"},Runs=new[]{"chase-1"},Idles=new[]{"idle-3"},Wanders=new[]{"walk-4"}}};
        private static PveAddStats Row(string id,string attack,float hp,float damage,float walk,float run,float reach,float start,float hit,float cycle,float lunge,float impulse,float reward,float limb,float head,float stagger,float radius)
            =>new PveAddStats{id=id,attack=attack,hp=hp,damage=damage,walk=walk,run=run,reach=reach,startRange=start,hit=hit,cycle=cycle,lunge=lunge,impulse=impulse,reward=reward,limbFrac=limb,headFrac=head,stagger=stagger,smashRadius=radius};

        // ---- indices: the five follow the Siren's kinds (Core/NativeHordeZones registration order) ----
        private static class At{internal static readonly int First=NativeHordeZones.First(Kinds);}
        /// <summary>The horde kind index of the cinderling (the others follow in the zone's order).</summary>
        public static int First=>At.First;
        public static int Kind(int local)=>At.First+local;
        /// <summary>The zone's local index of a horde kind (-1: not one of the five).</summary>
        public static int Local(int kind){int i=kind-At.First;return At.First>=0&&i>=0&&i<KindCount?i:-1;}
        public static bool Is(int kind)=>Local(kind)>=0;

        // ================================================================================================================
        // Telegraph timings (the doc's; every one from a replicated time)
        // ================================================================================================================
        // ---- Cinderling: the lunge (LastAttack + hit .38: crouch, the core flares white, a hiss), the pop on a melee death ----
        /// <summary>The pop: a melee kill sets the fuse (the core strobes white), then a 1.4 m ember burst.</summary>
        public const float PopFuse=.45f,PopRadius=1.4f;
        /// <summary>When a cinderling killed at `deathAt` pops (NaN: it did not die to a melee blow, so it only crumbles).</summary>
        public static float PopAt(float deathAt,bool meleeKill)=>meleeKill?deathAt+PopFuse:float.NaN;
        /// <summary>The core's strobe through the fuse (0..1; a beat that quickens to the burst).</summary>
        public static float Strobe(float sinceDeath){if(sinceDeath<0||sinceDeath>PopFuse)return 0;float u=sinceDeath/PopFuse;return .5f+.5f*(float)Math.Cos(Math.PI*2*(3*u+5*u*u));}

        // ---- Stoker: the rake sweep (LastAttack + .5), the hook pull (pullAt) ----
        public const float PullWindup=.7f,PullRange=4.5f,PullWidth=.6f,PullDrag=2.5f,PullDragTime=.35f,PullRoot=.4f,PullCooldown=6f,RakeLength=3f,SweepArc=2.6f;
        /// <summary>The hook's flight from the throw to the line's end (a whip: 4.5 m in .14 s).</summary>
        public const float HookFlight=.14f;
        public enum PullPhase{None,Windup,Throw,Drag,Root,Recover}
        /// <summary>The pull's phase `t - pullAt` seconds in, and the seconds into it; a hooked pilot is dragged in the Drag phase.</summary>
        public static PullPhase Pull(float pullAt,float t,bool hooked,out float into)
        {
            into=0;if(float.IsNaN(pullAt))return PullPhase.None;float a=t-pullAt;if(a<0)return PullPhase.None;
            if(a<PullWindup){into=a;return PullPhase.Windup;}a-=PullWindup;
            if(a<HookFlight){into=a;return PullPhase.Throw;}a-=HookFlight;
            if(hooked){if(a<PullDragTime){into=a;return PullPhase.Drag;}a-=PullDragTime;if(a<PullRoot){into=a;return PullPhase.Root;}a-=PullRoot;}
            else if(a<.3f){into=a;return PullPhase.Recover;}else return PullPhase.None;
            if(a<.45f){into=a;return PullPhase.Recover;}return PullPhase.None;
        }
        /// <summary>How far out the hook is along the 4.5 m line (m): drawn back over the shoulder in the wind-up, whipped out at the
        /// throw, reeled in with the drag (or after a miss).</summary>
        public static float HookOut(float pullAt,float t,bool hooked)
        {
            var ph=Pull(pullAt,t,hooked,out float into);
            switch(ph)
            {
                case PullPhase.Throw:{float u=into/HookFlight;return PullRange*(1-(1-u)*(1-u));}
                case PullPhase.Drag:return PullRange-PullDrag*Sm(into/PullDragTime);
                case PullPhase.Root:return PullRange-PullDrag;
                case PullPhase.Recover:return hooked?(PullRange-PullDrag)*(1-Sm(into/.45f)):PullRange*(1-Sm(into/.3f));
            }
            return 0;
        }
        /// <summary>The pull is stopped in its wind-up by a heavy melee blow or a hit of 15% of its health (the contract's rule).</summary>
        public static bool PullInterrupted(float amount,float maxHp,bool heavyMelee)=>amount>0&&(amount>=maxHp*.15f||heavyMelee);

        // ---- Slagback: the slam (LastAttack + .55), the shed (shedAt) ----
        public const float ShedStagger=.8f,ShedSpeed=1.25f,ArmourArc=120,ArmourCut=.6f,SlamRingDashes=28;
        /// <summary>The shed's stagger weight (1 at its start, eased out over .8 s).</summary>
        public static float Shedding(float shedAt,float t){if(float.IsNaN(shedAt))return 0;float a=t-shedAt;return a<0||a>ShedStagger?0:1-Sm(a/ShedStagger);}
        /// <summary>Whether its crust is still on at `t` (the plates: on until shedAt, flying for 1.6 s after).</summary>
        public static bool Crusted(float shedAt,float t)=>float.IsNaN(shedAt)||t<shedAt;
        /// <summary>Whether a hit from direction `push` (the authority's push, horizontal, away from the shooter) on an add facing
        /// `yaw` came from its armoured front 120 degrees (the deflect's spark and tink).</summary>
        public static bool Frontal(Vector2 push,float yaw)
        {
            if(push.LengthSquared()<1e-6f)return false;float r=yaw*(float)Math.PI/180;var fwd=new Vector2((float)Math.Sin(r),(float)Math.Cos(r));
            // the push points away from the shooter: a shot from the front pushes backward.
            return Vector2.Dot(Vector2.Normalize(push),-fwd)>=(float)Math.Cos(ArmourArc*.5f*Math.PI/180);
        }

        // ---- Ladleman: the pour (pourAt), the swing (LastAttack + .6), the spill (spilledAt) ----
        public const float PourWindup=1f,PourTime=1.2f,PourArc=60,PourRange=4.6f,PourInner=1.1f,PoolTime=3f,PoolCool=.8f,PourCooldown=7f,SpillStun=2f;
        public enum PourPhase{None,Raise,Pour,Lower}
        public static PourPhase Pour(float pourAt,float t,out float into)
        {
            into=0;if(float.IsNaN(pourAt))return PourPhase.None;float a=t-pourAt;if(a<0)return PourPhase.None;
            if(a<PourWindup){into=a;return PourPhase.Raise;}a-=PourWindup;if(a<PourTime){into=a;return PourPhase.Pour;}a-=PourTime;
            if(a<.6f){into=a;return PourPhase.Lower;}return PourPhase.None;
        }
        /// <summary>The ladle's tip (0 carried level .. 1 tipped right over) at `t`.</summary>
        public static float Tip(float pourAt,float t)
        {
            var ph=Pour(pourAt,t,out float into);
            switch(ph){case PourPhase.Raise:return -.18f*Sm(into/PourWindup);case PourPhase.Pour:return -.18f+1.18f*Sm(into/.3f);case PourPhase.Lower:return 1-Sm(into/.6f);}
            return 0;
        }
        /// <summary>Where the stream lands across the arc during the pour (-1 its left edge .. 1 its right), NaN out of the pour.</summary>
        public static float Sweep(float pourAt,float t){if(Pour(pourAt,t,out float into)!=PourPhase.Pour)return float.NaN;return -1+2*Sm(into/PourTime);}
        /// <summary>
        /// The molten pool the pour leaves (derived: its extent is the arc, laid as the stream sweeps; it burns 3 s after the pour
        /// ends, then cools out): how much of the arc is laid (0..1 from its left edge), its heat (1 white-hot .. 0 dark crust) and
        /// its opacity. False before the pour or once it has cooled.
        /// </summary>
        public static bool Pool(float pourAt,float t,out float laid,out float heat,out float alpha)
        {
            laid=heat=alpha=0;if(float.IsNaN(pourAt))return false;float a=t-pourAt-PourWindup;if(a<0)return false;float end=PourTime+PoolTime;if(a>end+PoolCool)return false;
            laid=Clamp01(a/PourTime);heat=a<end?1-.35f*Sm((a-PourTime)/PoolTime):(1-.35f)*(1-Sm((a-end)/PoolCool));alpha=a<end?Sm(a/.12f):1-Sm((a-end)/PoolCool);return true;
        }
        /// <summary>Whether a floor point (relative to the Ladleman, Unity world, m) lies in the pour's arc (yaw in degrees).</summary>
        public static bool InArc(Vector2 rel,float yaw)
        {
            float d=rel.Length();if(d<PourInner*.5f||d>PourRange)return false;float r=yaw*(float)Math.PI/180;var fwd=new Vector2((float)Math.Sin(r),(float)Math.Cos(r));
            return Vector2.Dot(rel/d,fwd)>=(float)Math.Cos(PourArc*.5f*Math.PI/180);
        }
        /// <summary>The spill: knocked to its knees for the stun (1 at the hit, eased back up in its last half second).</summary>
        public static float Spilled(float spilledAt,float t){if(float.IsNaN(spilledAt))return 0;float a=t-spilledAt;if(a<0||a>SpillStun+.5f)return 0;return Sm(a/.25f)*(1-Sm((a-SpillStun)/.5f));}
        /// <summary>Whether the ladle is still full and carried (no spill yet at `t`).</summary>
        public static bool Carrying(float spilledAt,float t)=>float.IsNaN(spilledAt)||t<spilledAt;

        // ---- Rigger: the hook drop (lockAt), the hook swing (LastAttack + .6), the climb (climbAt), the fall (chainCutAt) ----
        /// <summary>The hook drop (contract `hookDrop`): the rattle .4 s before the lock (T-2.0), the slide 1.25 s after it (T-.35),
        /// the landing at the lock + 1.6 (T 0); the hook hangs 2.4 m over the spot, swaying .6 m every .8 s; a 1.5 m ring.</summary>
        public const float DropRattle=.4f,DropSlide=1.25f,DropLand=1.6f,DropRecover=.5f,HookHeight=2.4f,HookSway=.6f,SwayPeriod=.8f,DropRadius=1.5f,HookFall=.24f;
        public const float SwingWindup=.6f,SwingSweep=.5f,SwingArc=300,SwingRadius=3.2f,SwingInner=1f;
        public const float ClimbThrow=.5f,ClimbSpeed=5f,ClimbReach=8f,Lurk=4f,FallStun=1.5f,Gravity=9.8f;
        /// <summary>The chain anchor over a hang point's feet (the girder: the draft's hook height less its feet height).</summary>
        public const float GirderAbove=2.4f;
        /// <summary>A hang entry's feet height over its landing (the entry's authored height, else 10.8 m, the casting floor's).</summary>
        public static float HangHeight(PveEntry e)=>e!=null&&MathUtil.Finite(e.height)&&e.height>1?e.height:10.8f;
        public enum DropPhase{None,Rattle,Lowered,Slide,Land,Done}
        /// <summary>The hook drop's phase at `t` for a lock at `lockAt` and the seconds into it (Rattle runs from T-2.0 to the lock).</summary>
        public static DropPhase Drop(float lockAt,float t,out float into)
        {
            into=0;if(float.IsNaN(lockAt))return DropPhase.None;float a=t-lockAt;
            if(a<-DropRattle)return DropPhase.None;if(a<0){into=a+DropRattle;return DropPhase.Rattle;}
            if(a<DropSlide){into=a;return DropPhase.Lowered;}if(a<DropLand){into=a-DropSlide;return DropPhase.Slide;}
            if(a<DropLand+DropRecover){into=a-DropLand;return DropPhase.Land;}return DropPhase.Done;
        }
        /// <summary>The hook's drop below the girder `drop` seconds after the lock: it falls out of the dark and stops with a jolt
        /// (0..1 of its way to <see cref="HookHeight"/> over the spot).</summary>
        public static float HookLowered(float sinceLock){if(sinceLock<0)return 0;float u=Clamp01(sinceLock/HookFall);return u*u*(1+.08f*(float)Math.Sin(Math.Min(1,(sinceLock-HookFall)/.3f)*Math.PI)*(sinceLock>HookFall?1:0));}
        /// <summary>The hook's sway over the spot (m, along the sway's line), a pendulum of .8 s; its shadow swings across the ring.</summary>
        public static float Sway(float sinceLock,uint seed){if(sinceLock<0)return 0;float settle=.5f+.5f*Sm(sinceLock/.6f);return HookSway*settle*(float)Math.Sin(Math.PI*2*sinceLock/SwayPeriod+NativeHordeKinds.Hash(seed,301)*.6f);}
        /// <summary>The landing ring's opacity (fades in with the lock, flashes white at the slide) and its flash (0..1).</summary>
        public static float Ring(float lockAt,float t,out float flash)
        {
            flash=0;var ph=Drop(lockAt,t,out float into);
            switch(ph){case DropPhase.Lowered:return Sm(into/.3f);case DropPhase.Slide:flash=1-Sm(into/(DropLand-DropSlide));return 1;case DropPhase.Land:flash=0;return 1-Sm(into/.25f);}
            return 0;
        }
        /// <summary>How far down the chain the Rigger is (0 at its hang .. 1 on the floor) during the slide.</summary>
        public static float Slid(float lockAt,float t){var ph=Drop(lockAt,t,out float into);if(ph==DropPhase.Slide){float u=into/(DropLand-DropSlide);return u*u;}return ph==DropPhase.Land||ph==DropPhase.Done?1:0;}
        /// <summary>Whether its head lamp is on: off while it lurks, on from the lock until it lands and fights, off once it dies.</summary>
        public static bool Lamp(float lockAt,float t,bool hanging)=>!hanging||(!float.IsNaN(lockAt)&&t>=lockAt);
        /// <summary>The swing (LastAttack + .6): the hook spun overhead in the wind-up (two loops), then swept 300 degrees from its
        /// front-left in .5 s. The hook's angle about the Rigger (rad from its forward, + to its left) and its radius (m).</summary>
        public static void Swing(float age,out float angle,out float radius,out bool sweeping)
        {
            sweeping=false;angle=0;radius=.9f;if(float.IsNaN(age)||age<0)return;
            if(age<SwingWindup){float u=age/SwingWindup;angle=(float)(Math.PI*2*2*u*u);radius=.75f+.25f*u;return;}
            float s=(age-SwingWindup)/SwingSweep;if(s<=1){sweeping=true;float arc=SwingArc*(float)Math.PI/180;angle=(float)(Math.PI*.5+arc*.5)-arc*Sm(s);radius=SwingRadius;return;}
            float back=Clamp01((s-1)*SwingSweep/.4f);angle=(float)(Math.PI*.5-SwingArc*Math.PI/360);radius=SwingRadius*(1-back)+.9f*back;
        }
        /// <summary>The climb: the hook thrown up (.5 s), then hand over hand at 5 m/s to the hang height; the share climbed (0..1).</summary>
        public static float Climbed(float climbAt,float t,float height,out bool throwing)
        {
            throwing=false;if(float.IsNaN(climbAt))return 0;float a=t-climbAt;if(a<0)return 0;if(a<ClimbThrow){throwing=true;return 0;}
            return Clamp01((a-ClimbThrow)*ClimbSpeed/Math.Max(.5f,height));
        }
        /// <summary>The fall when the chain is cut (or it is knocked off) from `height` m: its height over the floor and whether it
        /// has landed (then the 1.5 s stun).</summary>
        public static float Fallen(float cutAt,float t,float height,out bool landed,out float sinceLand)
        {
            landed=false;sinceLand=0;if(float.IsNaN(cutAt)||t<cutAt)return height;float a=t-cutAt,fall=(float)Math.Sqrt(2*Math.Max(0,height)/Gravity);
            if(a<fall)return height-.5f*Gravity*a*a;landed=true;sinceLand=a-fall;return 0;
        }
        /// <summary>The Rigger's entrance from a hang point (a ceiling vent the zone proposes for it): the same hook drop onto the
        /// entry's exit, landing <see cref="DropRecover"/> before ActiveAt (Astra's entry timeline: SpawnAt is the rattle).</summary>
        public const float EntryDuration=DropRattle+DropLand+DropRecover;
        public static float EntryLock(float activeAt)=>activeAt-DropRecover-DropLand;

        // ================================================================================================================
        // Audio: the zone's enemy cue keys (doc section 8; the owner's clips come later: the bank plays each when it has it)
        // ================================================================================================================
        public static string Cue(int local,string cue)=>local>=0&&local<KindCount?CueKeys[local*CueSlots+Array.IndexOf(CueNames[local],cue)]:null;
        public const int CueSlots=14;
        /// <summary>The cues of each kind (section 8's keys after "pve.ironworks.&lt;kind&gt;.").</summary>
        public static readonly string[][] CueNames={
            new[]{"hiss","screech","flare","pop"},
            new[]{"breath","rake","hook.windup","hook.throw","hook.yank"},
            new[]{"grind","slam.windup","slam","deflect","shed","emerge"},
            new[]{"carry","pour.windup","pour","swing","spill","groan"},
            new[]{"rattle","hang","lamp","lower","slide","land","swing.windup","swing","throw","climb","snap","groan","shriek"}};
        /// <summary>Every key, made once (no string per cue): slot local*14 + the cue's index.</summary>
        public static readonly string[] CueKeys=MakeKeys();
        private static string[] MakeKeys(){var k=new string[KindCount*CueSlots];for(int i=0;i<KindCount;i++)for(int c=0;c<CueNames[i].Length;c++)k[i*CueSlots+c]="pve.ironworks."+Ids[i]+"."+CueNames[i][c];return k;}
        public static int CueCount{get{int n=0;foreach(var c in CueNames)n+=c.Length;return n;}}

        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }
}
