using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Siren's minion kinds as the presentation draws them (owner 2026-10-12: "please improve animations of siren minions and
    /// design. Give them hitting from melee too. Add some variants (Like brutes, spitters, runners, etc.)"). Presentation only:
    /// nothing here is read by the authority. The rules stay NativePveHorde.cs and PveContent.json (Astra's); this mirrors the two
    /// things the presentation must follow exactly (each attack's strike and recovery times, and the hit volumes) and holds the
    /// proposed numbers docs/SIREN-MINION-VARIANTS.md hands Astra for the kinds the content does not have yet.
    /// <list type="bullet">
    /// <item>A kind resolves from the add kind string the network already carries (NativeMinionFrame.addKind, the content's
    /// PveAddStats.id): shambler, runner and brute ship today; spitter, crawler and bloater draw the moment the content names
    /// them; any other id draws as the shambler, so a newer host never leaves an add invisible.</item>
    /// <item>Each add takes one of its kind's looks by its id (<see cref="Look"/>), and its seed varies its posture, gait and
    /// glow phase (Core/NativeHordeMotion), so a crowd never moves in step.</item>
    /// <item>Strike and recovery are the authority's own formula (<see cref="Strike"/>, <see cref="Recover"/>): the claws, fists,
    /// glob or jaws arrive at LastAttack + hit and never at another time; Tests/HordeKindChecks reads the formula out of
    /// NativePveHorde.cs and the timings out of PveContent.json, so a rule change fails the build until this follows it.</item>
    /// <item>The hit volumes are the authority's capsules (<see cref="Radius"/>, <see cref="Height"/>; the crawl capsule once a leg
    /// is gone). The models are sculpted inside them (Core/NativeHordeModels) and the checks hold every posed body to them.</item>
    /// </list>
    /// </summary>
    public static class NativeHordeKinds
    {
        public const int Shambler=0,Runner=1,Brute=2,Spitter=3,Crawler=4,Bloater=5,Hulk=6,Count=7;
        /// <summary>The kinds the DEV preview's mix hashes over (not the Hulk: one or two are alive at most).</summary>
        public const int MixCount=Hulk;
        /// <summary>The content ids (PveAddStats.id), by kind.</summary>
        public static readonly string[] Ids={"shambler","runner","brute","spitter","crawler","bloater","hulk"};
        public static readonly string[] Names={"Shambler","Runner","Brute","Spitter","Crawler","Bloater","Hulk"};
        /// <summary>Kinds the shipped content spawns (the others are proposals until Astra adds them).</summary>
        public static readonly bool[] Shipped={true,true,true,true,false,false,true};
        /// <summary>The kind an add kind id draws as (an unknown or missing id: the shambler).</summary>
        public static int Resolve(string id){if(id!=null)for(int i=0;i<Count;i++)if(string.Equals(Ids[i],id,StringComparison.Ordinal))return i;return Shambler;}
        /// <summary>Whether an id names one of the kinds (no fallback).</summary>
        public static bool Known(string id){if(id!=null)for(int i=0;i<Count;i++)if(string.Equals(Ids[i],id,StringComparison.Ordinal))return true;return false;}
        /// <summary>The DEV preview's kind (looks only, presentation side; Settings · Developer and the Dev Sandbox gallery): -1 draws
        /// every add as its own kind, 0..6 every add as that kind, <see cref="Mix"/> every add as a kind hashed from its id.</summary>
        public static int Preview=-1;public const int Mix=-2;
        /// <summary>Looks per kind (whole sculpted variants; the seed varies each further).</summary>
        public static readonly int[] Looks={3,2,2,2,2,2,2};
        public const int MostLooks=3;
        /// <summary>An add's look seed: the minion fx seed (its id's digits), so the effects and the body agree.</summary>
        public static uint Seed(int id)=>NativeGoreRules.FnvDigits(id);
        public static float Hash(uint seed,int k)=>(float)NativeGoreRules.Hash01(seed,k,0x4d1);
        /// <summary>The look an add of `kind` wears, by its id (stable for its life, the same on every peer).</summary>
        public static int Look(int kind,int id){int n=kind>=0&&kind<Count?Looks[kind]:1;return Math.Min(n-1,(int)(Hash(Seed(id),1)*n));}

        // ---- hit volumes (NativePveHorde.SpawnPveAdd and the crawl change in HitPveAdd) ----
        /// <summary>The capsule every add spawns with today (radius, height), and the one an add that lost a leg crawls in.</summary>
        public const float AddRadius=.42f,AddHeight=1.7f,CrawlRadius=.32f,CrawlHeight=.88f;
        /// <summary>The capsule each kind is sculpted inside: the shipped kinds and the spitter use the add capsule; the bloater is
        /// built wide for the .5 m one the spec proposes, and the crawler long and low for a capsule laid along its facing
        /// (radius .32, 1.3 m long, its centre .36 m up and .25 m ahead of the root). The proposed kinds are drawn on the add
        /// capsule only in the DEV preview until the content has them.
        /// The Hulk is drawn <see cref="Scale"/> times the rig's size: its capsule here is the rig's (radius .53, height 1.7), so in
        /// the world it is the proposed radius .95, height 3.06 one (docs/SIREN-ZOMBIES.md; <see cref="WorldRadius"/>).</summary>
        public static readonly float[] Radius={AddRadius,AddRadius,AddRadius,AddRadius,.32f,.5f,HulkRadius/HulkScale},Height={AddHeight,AddHeight,AddHeight,AddHeight,1.3f,AddHeight,AddHeight};
        /// <summary>The capsule's axis (Unity CapsuleCollider.direction): 1 upright, 2 along the add's facing (the crawler).</summary>
        public static readonly int[] Axis={1,1,1,1,2,1,1};
        /// <summary>Where the rig root (the body's position) stands above the floor, in the rig: half the upright capsule, the crawler's centre height.</summary>
        public static readonly float[] Root={AddHeight*.5f,AddHeight*.5f,AddHeight*.5f,AddHeight*.5f,.36f,AddHeight*.5f,AddHeight*.5f};
        /// <summary>The capsule's centre ahead of the root (m): only the crawler's is offset, toward its head.</summary>
        public static readonly float[] CenterAhead={0,0,0,0,.25f,0,0};
        /// <summary>How many times the rig's size a kind is drawn (its rig root scaled; the gait runs in the rig's metres). The Hulk:
        /// 1.8, the brute's body grown to the proposed 3.06 m capsule.</summary>
        public static readonly float[] Scale={1,1,1,1,1,1,HulkScale};
        public const float HulkScale=1.8f,HulkRadius=.954f,HulkHeight=AddHeight*HulkScale;
        /// <summary>The capsule a kind is sculpted inside, in the world (m).</summary>
        public static float WorldRadius(int kind)=>Radius[Clamp(kind)]*Scale[Clamp(kind)];
        public static float WorldHeight(int kind)=>Height[Clamp(kind)]*Scale[Clamp(kind)];
        /// <summary>The floor under an add's root in the world: the authority's crawl capsule's half height once a leg is gone,
        /// else the kind's (the Hulk's root stands 1.53 m up its proposed capsule).</summary>
        public static float RootHeight(int kind,bool crawl)=>crawl?CrawlHeight*.5f:Root[Clamp(kind)]*Scale[Clamp(kind)];
        /// <summary>Where the drawn rig root stands over the floor (m, world): the rig's root height times the kind's scale (a crawling
        /// Hulk is drawn at its size over the authority's crawl capsule).</summary>
        public static float DrawRoot(int kind,bool crawl)=>(crawl?CrawlHeight*.5f:Root[Clamp(kind)])*Scale[Clamp(kind)];

        // ---- weight ----
        /// <summary>How heavy a kind feels to a hit (1 the shambler): the brute barely rocks, the runner is thrown about.</summary>
        public static readonly float[] Mass={1,.78f,2.6f,.95f,.85f,1.9f,9};
        /// <summary>A hit's shove and recoil on this kind (1/sqrt(mass), so a greatsword still moves a brute).</summary>
        public static float Give(int kind)=>1f/(float)Math.Sqrt(Mass[Clamp(kind)]);
        private static int Clamp(int kind)=>kind<0||kind>=Count?0:kind;

        // ---- the authority's attack timing (NativePveHorde.StepPveHorde) ----
        /// <summary>Both arms gone (LIMB bits 2|8 and 4|16 on each side).</summary>
        public static bool Armless(int lost)=>(lost&(2|8))!=0&&(lost&(4|16))!=0;
        public static bool LeftArmless(int lost)=>(lost&(2|8))!=0;
        public static bool RightArmless(int lost)=>(lost&(4|16))!=0;
        /// <summary>When the attack lands after LastAttack: the stats' hit, or .34 s crawling or armless (StepPveHorde's
        /// <c>hit=a.Crawl||armless?.34f:a.Stats.hit</c>; StrikeAt=LastAttack+hit).</summary>
        public static float Strike(PveAddStats stats,bool crawl,int lost)=>stats?.id=="spitter"?stats.hit:Strike(stats?.hit??.34f,crawl,lost);
        public static float Recover(PveAddStats stats,bool crawl,int lost)=>Math.Max(stats?.cycle??1,Strike(stats,crawl,lost)+.05f);
        public static float Strike(float hit,bool crawl,int lost)=>crawl||Armless(lost)?.34f:hit;
        /// <summary>When the add can move again after LastAttack (<c>RecoverAt=Time+Math.Max(a.Stats.cycle,hit+.05f)</c>).</summary>
        public static float Recover(float hit,float cycle,bool crawl,int lost)=>Math.Max(cycle,Strike(hit,crawl,lost)+.05f);
        /// <summary>How far the add carries itself forward in the wind-up (the lunge; .15 crawling, .45 armless).</summary>
        public static float Lunge(float lunge,bool crawl,int lost)=>crawl?.15f:Armless(lost)?.45f:lunge;
        /// <summary>The smash's area (PveAddStrike): a disc of radius 1.6 m whose centre is 1.2 m ahead of the add.</summary>
        public const float SmashAhead=1.2f,SmashRadius=1.6f;
        /// <summary>The Hulk's proposed smash (docs/SIREN-ZOMBIES.md): a 2.8 m disc 2 m ahead.</summary>
        public const float HulkSmashAhead=2f,HulkSmashRadius=2.8f;
        /// <summary>The smash disc a kind presents: the authority's for every kind, the Hulk's proposed one for the Hulk.</summary>
        public static float SmashAheadOf(int kind)=>kind==Hulk?HulkSmashAhead:SmashAhead;
        public static float SmashRadiusOf(int kind)=>kind==Hulk?HulkSmashRadius:SmashRadius;
        /// <summary>
        /// Whether a hit cancels the add's wind-up (the presentation's reading until the authority replicates its StaggerAt): a
        /// stagger always does (a quarter of its health, or a heavy melee blow; StepPveHorde), and any damaging hit interrupts a
        /// spitter's spit (the proposed rule, docs/SIREN-ZOMBIES.md). The Hulk shrugs off everything but a stagger.
        /// </summary>
        public static bool Interrupts(int kind,Attack attack,float amount,float maxHp,bool heavyMelee)=>amount>0&&(Staggers(kind,amount,maxHp,heavyMelee)||kind!=Hulk&&attack==Attack.Spit);
        /// <summary>Whether a hit staggers the add as the presentation shows it (a quarter of its health or a heavy melee blow; the
        /// Hulk only from a quarter of its health: everything else it walks through).</summary>
        public static bool Staggers(int kind,float amount,float maxHp,bool heavyMelee)=>amount>0&&(amount>=maxHp*.25f||heavyMelee&&kind!=Hulk);
        /// <summary>How the content's attack name (PveAddStats.attack) is presented; a kind keeps its own when the content has none.</summary>
        public enum Attack{Swipe,Lunge,Smash,Spit,Pounce,Belly}
        public static readonly Attack[] Default={Attack.Swipe,Attack.Lunge,Attack.Smash,Attack.Spit,Attack.Pounce,Attack.Belly,Attack.Smash};
        public static Attack AttackOf(int kind,string attack,bool crawl)
        {
            if(kind==Spitter)return Attack.Spit;
            if(crawl&&kind!=Crawler)return Attack.Lunge;
            switch(attack){case "swipe":return Attack.Swipe;case "lunge":return kind==Crawler?Attack.Pounce:Attack.Lunge;case "smash":return Attack.Smash;case "spit":return Attack.Spit;case "pounce":return Attack.Pounce;case "belly":case "burst":return Attack.Belly;}
            return Default[Clamp(kind)];
        }

        // ---- the spec's proposed stats (docs/SIREN-MINION-VARIANTS.md; the DEV preview times its attacks with them) ----
        /// <summary>Proposed content rows for the new kinds, and today's rows for the shipped ones (the preview gallery's timing
        /// when no content is loaded). Astra owns the real numbers.</summary>
        public static readonly PveAddStats[] Proposed={
            Row("shambler","swipe",60,6,1.5f,3.4f,1.75f,1.45f,.34f,1.05f,.45f,90,25,.35f,.4f,.45f,0),
            Row("runner","lunge",38,4,2.6f,4.6f,2,2.2f,.34f,.85f,.9f,60,20,.35f,.4f,.45f,0),
            Row("brute","smash",240,15,1.3f,2.4f,2.1f,1.7f,.43f,1.7f,.3f,420,80,.45f,.5f,.25f,SmashRadius),
            Row("spitter","spit",45,7,1.6f,2.9f,11,9,SpitHit,2.6f,0,40,30,.35f,.4f,.45f,1.3f),
            Row("crawler","pounce",34,5,2.2f,3.9f,1.6f,2.4f,.3f,1.1f,1.6f,70,20,.3f,.35f,.5f,0),
            Row("bloater","belly",150,10,1.1f,1.8f,1.6f,1.4f,.62f,2.2f,.2f,180,45,.4f,.45f,.3f,2.6f),
            Row("hulk","smash",1600,32,.75f,1.15f,3,2.6f,1.1f,3.4f,.15f,900,300,.55f,.6f,.2f,HulkSmashRadius)};
        /// <summary>The spitter's proposed wind-up (s from LastAttack to the glob leaving the mouth): long enough to read and to hit.</summary>
        public const float SpitHit=.8f;
        private static PveAddStats Row(string id,string attack,float hp,float damage,float walk,float run,float reach,float start,float hit,float cycle,float lunge,float impulse,float reward,float limb,float head,float stagger,float radius)
            =>new PveAddStats{id=id,attack=attack,hp=hp,damage=damage,walk=walk,run=run,reach=reach,startRange=start,hit=hit,cycle=cycle,lunge=lunge,impulse=impulse,reward=reward,limbFrac=limb,headFrac=head,stagger=stagger,smashRadius=radius};
        public static PveAddStats ProposedRow(int kind)=>Proposed[Clamp(kind)];

        // ---- look ----
        /// <summary>Each kind's seam glow (sRGB): the Siren's violet for the shambler, then a hue per kind so a crowd reads at 30 m.</summary>
        public static readonly int[] Seam={0x9d84ff,0xff4fb4,0xff6a2c,0x8dff4f,0xe8dcff,0xffc23a,0xff2e4d};
        /// <summary>The flicker class of each kind's seams (NativeMinionFx.ClassFlicker): strips, the brute's embers steadier.</summary>
        public static readonly int[] SeamClass={2,2,1,2,2,1,1};

        // ---- cues (the attack layers: existing effects bank keys, played when the owner's LocalAssets bank has them) ----
        /// <summary>The wind-up's cue (the telegraph), heard at the add: null where the attack's own strike carries it.</summary>
        public static string WindupCue(Attack a)=>a==Attack.Smash?"structure.groan":a==Attack.Spit||a==Attack.Belly?"field.acid":null;
        /// <summary>The strike's cue at LastAttack + hit.</summary>
        public static string StrikeCue(Attack a)=>a==Attack.Smash?"siren.slam":a==Attack.Lunge||a==Attack.Pounce?"siren.rush":a==Attack.Spit?"magic.darkness.shot":a==Attack.Belly?"blast.toxic":"siren.claw";
        /// <summary>A second cue under the strike (the brute's floor hit), or null.</summary>
        public static string StrikeUnder(Attack a)=>a==Attack.Smash?"impact.ground":null;
        /// <summary>A kind's walking footfall key (Core NativeZombieSteps: the heavy kinds stomp, the crawler pulls, the rest shuffle;
        /// each footfall picks its own by how the add moves). Every kind steps, from its gait's plants.</summary>
        public static string StepCue(int kind)=>NativeZombieSteps.Key(kind,kind==Crawler,0);
        /// <summary>Cue gains (the bank's own trims apply on top).</summary>
        public const float WindupGain=.55f,StrikeGain=.7f,UnderGain=.8f;
    }
}
