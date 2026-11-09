using System;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Top Dog's names and numbers (docs/TOP-DOG.md): the owner's DEV class for the Hot Dog (2026-11-08), "toon power" with a cartoon mallet,
    /// abilities on Q, E and 1 to 5 and the ultimate, All You Can Eat, on 6. The rules (Runtime NativeTopDogAuthority.cs) and the presentation
    /// both read these, so a number lives in one place. Pure.
    /// </summary>
    public static class NativeTopDog
    {
        public const string Spec="topdog",Entrance="dev-topdog",Name="Top Dog";
        /// <summary>The burst a pilot All You Can Eat pops dies in (NativeSmashGib.Burst), named for its sound (NativeSoundCues "death.style.gib").</summary>
        public const string Burst="hot-dog-burst";
        /// <summary>The pop's death (NativeDeath.damageKind, replicated with the record; owner 2026-11-09: "use the kill effect if applicable (Like
        /// hot dog friends should work)"): the record keeps the killer's kill effect as its style, and this kind says the body burst, so the
        /// burst wears that effect where it can (NativeSmashGib.BurstStyle).</summary>
        public const string PopKind="topdog-pop";
        public static bool Popped(string damageKind)=>damageKind==PopKind;
        public const string Mallet="big-mallet",Boing="boing",Zip="zip",Anvil="anvil",Peel="banana-peel",Mustard="mustard",Glove="spring-glove",Pie="cream-pie",Feast="all-you-can-eat";
        // Prop kinds (NativeDogProp.Kind), marks (NativeDogMark.Kind) and hit kinds (NativeDogHit.Kind).
        public const string PropAnvil="anvil",PropPeel="peel",PropPie="pie",PropGlove="glove",PropSquirt="squirt",PropPuddle="puddle",PropSpring="spring",PropZip="zip",PropPiano="piano",PropSnack="snack";
        public const string Pancake="pancake",Slipped="slip",Pied="pied",Mustarded="mustard",Dazed="dazed",Stuffed="stuffed",Sauced="sauced",Walled="walled";
        public const string HitBonk="bonk",HitSlam="slam",HitThud="thud",HitSwat="swat",HitAnvil="anvil",HitSlip="slip",HitSplat="splat",HitPow="pow",HitPie="pie",HitGulp="gulp",HitPop="pop",HitSnap="snap",HitBoing="boing",HitClang="clang",HitPiano="piano",HitWall="wall",HitMunch="munch",HitToonFlat="toon-flat",HitToonPop="toon-pop";
        public const int MaxProps=32,MaxHits=48,MaxMarks=24,Slots=7,PeelCharges=2,MaxPeels=3,Feeds=6;
        /// <summary>The most peels in hand: two, and Banana Bonanza's (Core NativeDogChips).</summary>
        public const int MaxPeelCharges=PeelCharges+NativeDogChips.BananaMax*NativeDogChips.BananaCharges;
        /// <summary>The ability cards in their HUD order with their keys (the ultimate is apart, on 6).</summary>
        public static readonly string[] Slot={Boing,Zip,Anvil,Peel,Mustard,Glove,Pie};
        public static readonly string[] SlotKeys={"Q","E","1","2","3","4","5"};
        public const string UltimateKey="6";
        /// <summary>Cooldowns (s) in Slot order; the peel's is per charge.</summary>
        public static readonly float[] Cooldown={8,7,10,6,11,9,12};
        public static int SlotOf(string id)=>Array.IndexOf(Slot,id);
        public static bool IsKind(string k)=>k==PropAnvil||k==PropPeel||k==PropPie||k==PropGlove||k==PropSquirt||k==PropPuddle||k==PropSpring||k==PropZip||k==PropPiano||k==PropSnack;
        public static bool IsMark(string k)=>k==Pancake||k==Slipped||k==Pied||k==Mustarded||k==Dazed||k==Stuffed||k==Sauced||k==Walled;
        public static string AbilityName(string id)
        {
            switch(id){case Mallet:return "Big Mallet";case Boing:return "Boing!";case Zip:return "Zip";case Anvil:return "Anvil";case Peel:return "Banana Peel";case Mustard:return "Mustard Blaster";case Glove:return "Spring Glove";case Pie:return "Cream Pie";case Feast:return "All You Can Eat";case Dash:return "Mallet Dash";case Slam:return "Mallet Slam";case Smash:return "Mallet Smash";default:return "";}
        }
        /// <summary>The icon id an ability's card draws (the Top Dog's own glyphs, <see cref="Glyph"/>).</summary>
        public static string Icon(string id)=>"dog:"+(string.IsNullOrEmpty(id)?Mallet:id);

        // ---- the mallet ----
        /// <summary>The mallet's three swings: wind-up to the hit (s), the whole swing (s), damage, reach (m) and arc (half angle, rad). The third
        /// is the overhead slam: its hit is a ring round where the head lands (SlamAhead in front, SlamRadius round).</summary>
        public static readonly float[] SwingHit={.2f,.2f,.36f},SwingLength={.48f,.48f,.82f},SwingDamage={14,14,26},SwingReach={2.9f,2.9f,2.6f},SwingArc={1.0f,1.0f,.6f};
        public const float ComboWindow=.45f,SlamAhead=1.7f,SlamRadius=2.1f,PancakeSeconds=.8f,SwingPush=5;

        // ---- the mallet's dash and slam (owner 2026-11-09: "Give them a dash sprint attack and slam attack as well") ----
        /// <summary>The dash, the mallet's attack on a sprint (NativeMeleeFeel.SprintAttack, as any melee weapon's): tucked for DashWind s,
        /// then the Top Dog charges DashReach m along its facing in DashTime s (a wall stops it short), the mallet swung low through everyone
        /// within DashWidth m of its path ahead of it, who are bonked for DashDamage and batted on ahead at DashPush m/s and DashLift up;
        /// DashRecover s to stand. The slam, the mallet's attack in the air (once an airtime, NativeAirAttackSimulation.MinimumAir into it,
        /// a Boing!'s too): the Top Dog hangs SlamHang s with the mallet raised, then drops straight down at SlamFall m/s (SlamMaxFall s at
        /// most) and slams the floor: SlamDamage to everyone within SlamRing m, those within SlamFlat m flattened for SlamFlatFor s, the
        /// rest thrown SlamPush m/s out and SlamLift up; SlamRecover s to stand. Its CastStep is 1 once it has landed, CastUntil then
        /// SlamRecover after the landing (SlamLanded).</summary>
        public const string Dash="mallet-dash",Slam="mallet-slam",HitDash="dash",HitQuake="quake";
        public const float DashWind=.12f,DashTime=.34f,DashReach=6.5f,DashWidth=1.4f,DashDamage=20,DashPush=11,DashLift=5,DashRecover=.32f;
        public const float SlamHang=.2f,SlamFall=26,SlamMaxFall=2.5f,SlamRing=3.2f,SlamFlat=2.2f,SlamDamage=30,SlamFlatFor=.9f,SlamPush=5,SlamLift=6,SlamRecover=.42f;
        /// <summary>The slam's landing moment from its record (-100 while it is still in the air, or another cast).</summary>
        public static float SlamLanded(NativeTopDogKit k)=>k!=null&&k.CastKind==Slam&&k.CastStep==1?k.CastUntil-SlamRecover:-100;
        /// <summary>The smash (owner 2026-11-09: "make slam attack and overhead slam (When jumping and looking down)"): the mallet's attack in
        /// the air looking ahead (the slam is looking down: NativeAirAttackSimulation.DownPitch, as any melee weapon's plunge). Hung SmashHang
        /// s with the mallet cocked over the shoulder, then the Top Dog drops at SmashFall m/s carried on along its facing at SmashCarry m/s
        /// (its run's speed when faster, up to SmashCarryMax; SmashMaxFall s at most), the mallet swung down in front; the landing slams its
        /// head onto the floor SmashAhead m ahead: SmashDamage to everyone within SmashRing m of that point, those within SmashFlat m
        /// flattened for SmashFlatFor s, the rest thrown on SmashPush m/s and SmashLift up; SmashRecover s to stand. Its CastStep is 1 once
        /// landed (Landed).</summary>
        public const string Smash="mallet-smash";
        public const float SmashHang=.14f,SmashFall=16,SmashCarry=4,SmashCarryMax=9,SmashMaxFall=2.5f,SmashAhead=1.6f,SmashRing=2.4f,SmashFlat=1.4f,SmashDamage=24,SmashFlatFor=.7f,SmashPush=7,SmashLift=5,SmashRecover=.36f;
        /// <summary>The slam's or the smash's landing moment from its record (-100 while in the air, or another cast).</summary>
        public static float Landed(NativeTopDogKit k)=>k==null||k.CastStep!=1?-100:k.CastKind==Slam?k.CastUntil-SlamRecover:k.CastKind==Smash?k.CastUntil-SmashRecover:-100;
        /// <summary>One of the mallet's own moves (a swing of the combo, the dash, the smash, the slam): the mallet stays in hand.</summary>
        public static bool MalletMove(string kind)=>kind==Mallet||kind==Dash||kind==Slam||kind==Smash;

        // ---- the abilities ----
        public const float BoingRelease=.12f,BoingHeight=9,BoingAlong=8,ThudRadius=2.6f,ThudDamage=8,ThudLift=6;
        public const float ZipWind=.3f,ZipTime=.3f,ZipReach=12,ZipWidth=1.3f,ZipDamage=10,ZipPush=9;
        public const float AnvilRelease=.22f,AnvilFall=.9f,AnvilReach=22,AnvilRadius=2.1f,AnvilDamage=28,AnvilPancake=1.1f,AnvilStays=3.5f,AnvilHeight=14;
        public const float PeelRelease=.18f,PeelFlight=.45f,PeelReach=10,PeelLife=20,PeelTouch=.75f,PeelDamage=8,PeelDown=1.2f;
        public const float MustardTime=1.6f,MustardTick=.1f,MustardReach=9,MustardArc=.38f,MustardDamage=2,MustardSlow=.55f,MustardSlowFor=1.8f,PuddleEvery=.4f,PuddleRadius=1.2f,PuddleLife=4,PuddleSlow=.6f;
        public const float GloveRelease=.18f,GloveOut=.2f,GloveBack=.25f,GloveReach=13,GloveRadius=.45f,GloveDamage=18,GlovePush=15,GloveLift=4,GloveDaze=.7f;
        public const float PieRelease=.2f,PieSpeed=26,PieGravity=12,PieRadius=.35f,PieDamage=8,PieFor=2.6f,PieSplash=1.2f,PieSplashDamage=4,PieMaxFlight=1.6f;
        public const float CastRecover=.4f;

        // ---- All You Can Eat ----
        /// <summary>Its timeline: the twirl, the throw, the lasso's flight, the reel, the feeding (one every FeedPeriod), the let-go, the pop.</summary>
        public const float Twirl=.6f,LassoSpeed=32,LassoReach=18,LassoRadius=.8f,Reel=.5f,FeedDelay=.55f,FeedPeriod=.5f,Swallow=.4f,ReleaseAfter=.2f,Stagger=.45f,PopAfter=1.5f,After=.6f;
        public const float HoldAhead=.9f,PushBack=1.8f,FeedDamage=3,SnapDamage=40,SnapPerDog=6,PopDamage=22,PopRadius=3.2f,PopLift=8,PopPush=9,BossDamage=30,MissRefund=.5f;
        /// <summary>The meter: a second, a mallet hit, an ability hit (shares of a full meter).</summary>
        public const float MeterPerSecond=.01f,MeterMallet=.06f,MeterAbility=.08f;
        /// <summary>The feeding's moments from a catch at `catchAt`.</summary>
        /// <summary>The hand feeding (owner 2026-11-09: "hand feed the hotdogs into the players mouth (without the bun)... wiggle outside of the
        /// players mouth for a good few seconds"; "literally animate opening the players mouth and just putting hotdogs in there"): the left
        /// hand takes the victim's chin and holds the jaw open from JawFrom before the feeding to the let-go, tugging it on each push; the
        /// right hand comes in GrabLead before the first, and for each a bare sausage pops into it out of hammerspace Appear before its
        /// offer, it is brought to the lips from CarryFrom before the offer, pushed half in from PushFrom to PushEnd after it, and the hand backs off for the next. Each stays half
        /// out, squirming, StickFor, then is slurped down over Slurp; a snap spits them out over Spit. The fan: each one's angle out of the
        /// mouth (degrees).</summary>
        public const float GrabLead=.3f,Appear=.18f,CarryFrom=.1f,PushFrom=.08f,PushEnd=.3f,StickFor=2.6f,Slurp=.35f,Spit=.7f,LetGo=.22f,JawFrom=.35f;
        public static readonly float[] FanYaw={-20,16,-6,26,-30,8},FanPitch={10,-6,18,2,-12,14};
        public static float FeedAt(float catchAt)=>catchAt+FeedDelay;
        public static float ReleaseAt(float feedAt)=>feedAt+Feeds*FeedPeriod+ReleaseAfter;
        public static float PopAt(float release)=>release+PopAfter;
        /// <summary>When hot dog `i` (0..Feeds-1) is offered, and when it is down.</summary>
        public static float OfferAt(float feedAt,int i)=>feedAt+i*FeedPeriod;
        public static float DownAt(float feedAt,int i)=>OfferAt(feedAt,i)+Swallow;

        /// <summary>How full the victim is (0 nothing, 1 every hot dog down) at `time`: each swallow fills a share, eased in as it goes down.</summary>
        public static float Fill(NativeDogFeast f,float time)
        {
            if(f==null||f.Catch<-50)return 0;float fill=0;
            for(int i=0;i<Feeds;i++){float u=(time-OfferAt(f.FeedAt,i)-Swallow*.55f)/(Swallow*.6f);if(u<=0)break;fill+=Smooth(u)/Feeds;}
            if(f.Snap>-50&&time>=f.Snap){float u=(time-f.Snap)/.5f;fill*=1-Smooth(u);}
            return Math.Max(0,Math.Min(1,fill));
        }
        /// <summary>The belly's size (0 none, about 1 when every hot dog is down, up to Over at the pop): the fill, then once let go it keeps
        /// ballooning toward the pop, trembling faster as it goes.</summary>
        public const float Over=1.7f;
        public static float Belly(NativeDogFeast f,float time)
        {
            if(f==null||f.Catch<-50)return 0;float b=Fill(f,time);
            if(f.Snap<-50&&time>=f.Release){float u=Math.Max(0,Math.Min(1,(time-f.Release)/Math.Max(.1f,f.Pop-f.Release)));b=1+(Over-1)*u*u*(3-2*u)*.85f+(Over-1)*.15f*u*u*u*u;}
            if(f.Snap<-50&&time>=f.Pop)return 0;
            return b;
        }
        /// <summary>The swelling's tremble (a share of the size, signed) at `time`: a wobble once let go that quickens to the pop.</summary>
        public static float Tremble(NativeDogFeast f,float time)
        {
            if(f==null||f.Snap>-50||time<f.Release||time>=f.Pop)return 0;float u=(time-f.Release)/Math.Max(.1f,f.Pop-f.Release);
            double phase=(time-f.Release)*(9+30*u*u)*2*Math.PI;return (float)Math.Sin(phase)*(.025f+.06f*u*u);
        }
        /// <summary>How much the links hold the victim at `time` (0..1): pinned from the catch over BindIn, let go over BindOut to the
        /// release, nothing once spat out (owner 2026-11-09: "give more of a visual effect when the character is wrapped by sausaged, like
        /// their arms are downwards, and they're struggling to get out").</summary>
        public const float BindIn=.2f,BindOut=.12f;
        public static float Bound(NativeDogFeast f,float time)
        {
            if(f==null||f.Catch<-50||time<f.Catch||time>=f.Release||f.Snap>-50&&time>=f.Snap)return 0;
            return Smooth((time-f.Catch)/BindIn)*Smooth((f.Release-time)/BindOut);
        }
        /// <summary>How hard the wrapped victim strains against the links at `time` (0..1, in jerks, hardest at first), and `side` which way
        /// it throws its weight (+1 its right arm pushing out, -1 its left, between as it swaps).</summary>
        public static float Strain(NativeDogFeast f,float time,out float side)
        {
            side=0;float bound=Bound(f,time);if(bound<=0)return 0;double t=time-f.Catch;
            double a=Math.Max(0,Math.Sin(t*7.3)),b=Math.Max(0,Math.Sin(t*11.1+1.3));side=(float)Math.Sin(t*3.65);
            float strain=(float)Math.Min(1,a*a*a+.6*b*b*b*b)*(1+.5f*(float)Math.Exp(-t*1.5))/1.5f;
            return bound*Math.Min(1,strain*1.25f);
        }
        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}

        // ---- the ability glyphs: flat white shapes in a 100 box (Runtime NativeIcons draws them where a card asks for "dog:<id>") ----
        public const float GlyphBox=100;
        private static string F(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        private static string Box(float x0,float y0,float x1,float y1)=>"M"+F(x0)+" "+F(y0)+"L"+F(x1)+" "+F(y0)+"L"+F(x1)+" "+F(y1)+"L"+F(x0)+" "+F(y1)+"Z";
        private static string Oval(float x,float y,float rx,float ry){return "M"+F(x-rx)+" "+F(y)+"A"+F(rx)+" "+F(ry)+" 0 1 1 "+F(x+rx)+" "+F(y)+"A"+F(rx)+" "+F(ry)+" 0 1 1 "+F(x-rx)+" "+F(y)+"Z";}
        private static string Star(float x,float y,float r)=>"M"+F(x)+" "+F(y-r)+"L"+F(x+r*.3f)+" "+F(y-r*.3f)+"L"+F(x+r)+" "+F(y)+"L"+F(x+r*.3f)+" "+F(y+r*.3f)+"L"+F(x)+" "+F(y+r)+"L"+F(x-r*.3f)+" "+F(y+r*.3f)+"L"+F(x-r)+" "+F(y)+"L"+F(x-r*.3f)+" "+F(y-r*.3f)+"Z";
        private static readonly string[] glyphIds={Mallet,Boing,Zip,Anvil,Peel,Mustard,Glove,Pie,Feast};
        private static readonly string[] glyphs=new string[9];
        /// <summary>Icon `id`'s markup ("dog:" and an ability id), or null when it is not one of the Top Dog's.</summary>
        public static string Glyph(string id)
        {
            if(NativeDogChips.Is(id))return NativeDogChips.Glyph(id);
            if(id==null||!id.StartsWith("dog:",StringComparison.Ordinal))return null;int i=Array.IndexOf(glyphIds,id.Substring(4));if(i<0)return null;
            return glyphs[i]??(glyphs[i]="<path d=\""+GlyphPath(i)+"\" fill=\"#fff\"/>");
        }
        private static string GlyphPath(int i)
        {
            switch(i)
            {
                // The mallet: a barrel head, tilted, its handle down to the corner, a star where it lands.
                case 0:return "M38 12L78 52L64 66L24 26Z"+"M47 49L54 56L18 92L11 85Z"+Star(84,82,12);
                // The spring, a foot on top.
                case 1:return Box(22,84,78,92)+"M26 76L74 70L74 62L26 68Z"+"M26 58L74 52L74 44L26 50Z"+"M26 40L74 34L74 26L26 32Z"+"M30 22Q30 8 46 8L70 8Q80 8 80 18L80 22Z";
                // Speed lines behind a rushing blur.
                case 2:return Oval(68,50,22,22)+Box(8,30,40,36)+Box(2,47,40,53)+Box(12,64,40,70);
                // An anvil, its horn to the left, falling lines above it.
                case 3:return "M8 40L34 40L34 36L84 36L84 54Q70 56 66 64L72 76L84 76L84 86L22 86L22 76L34 76L40 64Q36 56 20 52Z"+Box(46,6,52,26)+Box(60,12,66,28);
                // A banana peel, splayed.
                case 4:return Oval(50,58,9,16)+"M44 50Q20 54 8 82Q26 74 42 62Z"+"M56 50Q80 54 92 82Q74 74 58 62Z"+"M46 70Q36 84 32 96Q46 88 50 74Z"+"M50 46Q52 26 62 12L66 14Q58 28 56 46Z";
                // A squeeze bottle squirting.
                case 5:return "M30 40Q30 34 36 34L56 34Q62 34 62 40L62 92Q62 96 58 96L34 96Q30 96 30 92Z"+"M38 22L54 22L56 34L36 34Z"+"M44 8L48 8L48 22L44 22Z"+"M52 6Q70 10 92 4L92 10Q70 16 52 12Z";
                // A boxing glove on its scissor arm.
                case 6:return Oval(68,46,24,26)+Box(40,38,48,58)+"M6 40L20 58L26 52L12 34Z"+"M6 58L20 40L26 46L12 64Z"+"M22 40L36 58L40 52L28 36Z";
                // A cream pie, its cherry.
                case 7:return "M10 62L90 62L82 82L18 82Z"+"M14 60Q20 38 50 38Q80 38 86 60Z"+Oval(50,30,7,7);
                // A hot dog in its bun, wiggling, stars round it.
                default:return "M10 52Q30 40 50 50Q70 60 90 48L90 58Q70 70 50 60Q30 50 10 62Z"+"M14 60Q50 86 86 58L86 66Q50 96 14 68Z"+Star(20,22,10)+Star(80,18,8)+Star(52,12,6);
            }
        }
    }
}
