using System;
using System.Globalization;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Top Dog's own ten chips (owner 2026-11-08: "exclusive chips for this character; about 10 of them with their own really well
    /// animated effects when selected. Make them significant effects too"; docs/TOP-DOG.md "The chips"). A signature set like the
    /// Bombadier's: its own tier, tag and odds, offered only to the Top Dog. Runtime NativeTopDogAuthority implements the mechanics (the
    /// owner chose that Claude builds this class's rules), Core NativeTopDogFx draws them in the world, Runtime NativeDogChipArt the card
    /// and the pick. Apply installs the rows over the live content, as NativeBombChips does.
    /// </summary>
    public static class NativeDogChips
    {
        public const string Tier="topdog",Prefix="dog-",Color="#e8322b";
        public const string BiggerMallet="dog-bigger-mallet",Hammerspace="dog-hammerspace",AnvilShower="dog-anvil-shower",BananaBonanza="dog-banana-bonanza",HotSauce="dog-hot-sauce",
            PieVolley="dog-pie-volley",Knockout="dog-knockout",DoubleBoing="dog-double-boing",SecondHelpings="dog-second-helpings",ToonLogic="dog-toon-logic";
        public static readonly string[] Ids={BiggerMallet,Hammerspace,AnvilShower,BananaBonanza,HotSauce,PieVolley,Knockout,DoubleBoing,SecondHelpings,ToonLogic};
        public static bool Is(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal)&&Array.IndexOf(Ids,id)>=0;
        public static int Index(string id)=>Array.IndexOf(Ids,id);
        /// <summary>How long the draft holds on a pick of one of these (s): the plant, then its gag (Runtime NativeDogChipArt.Reveal) to the end.</summary>
        public const double RevealSeconds=5.2;

        // ---- the numbers (the rules', the effects' and the cards') ----
        /// <summary>Bigger Mallet, a copy: the head's size, the swings' reach (m) and arc (rad), the slam's radius (m), the mallet's damage.</summary>
        public const float MalletGrow=.25f,MalletReach=.5f,MalletArc=.17f,MalletSlam=.5f,MalletDamage=.12f;
        public const int MalletMax=2,BananaMax=2;
        /// <summary>Hammerspace: every this many mallet hits, a piano on the one struck: its fall, radius, damage and how long it flattens.</summary>
        public const int PianoEvery=4;
        public const float PianoFall=.55f,PianoRadius=2.3f,PianoDamage=30,PianoFlat=1.2f,PianoStays=2.2f;
        /// <summary>Anvil Shower: the two more, each side across the aim, a beat apart.</summary>
        public const float ShowerSide=2.6f,ShowerGap=.18f,ShowerDamage=20,ShowerFlat=.8f;
        /// <summary>Banana Bonanza, a copy: charges and peels out; the slide's length; the peel flies on with them.</summary>
        public const int BananaCharges=1,BananaOut=2;
        public const float BananaSlide=1.6f,BananaReland=.9f;
        /// <summary>Hot Sauce: the stream's damage, the burn (a second, seconds), the puddles' burn.</summary>
        public const float SauceDamage=1.3f,SauceBurn=8,SauceBurnFor=3,SaucePuddle=6;
        /// <summary>Pie Volley: three pies, this far apart (rad); cream lasts longer.</summary>
        public const int Pies=3;
        public const float PieSpread=.17f,PieCream=1.5f;
        /// <summary>Knockout Glove: the reach (m), the wall splat's damage and stun, how soon after the punch a wall counts.</summary>
        public const float KnockReach=17,SplatDamage=15,SplatStun=1,SplatWindow=.6f;
        /// <summary>Double Boing: the second bounce's window; the landing's radius and flatten.</summary>
        public const float BoingWindow=3,BoingRadius=3.2f,BoingFlat=.6f,BoingAgain=.35f;
        /// <summary>Second Helpings: the meter, the lasso's width and reach, the snacks thrown out by the pop and what each heals.</summary>
        public const float HelpingMeter=1.35f,HelpingWidth=1.6f,HelpingReach=24,SnackHeal=8,SnackLife=10,SnackTouch=1;
        public const int Snacks=6;
        /// <summary>Toon Logic: flat (untouchable) this long, back with this share of health, the spring back's ring.</summary>
        public const float ToonFlat=1.5f,ToonHealth=.35f,ToonRadius=3,ToonDamage=20;

        /// <summary>The rule each chip needs (all of them: none is a plain stat).</summary>
        public static readonly string[] Mechanics=Ids;
        private static ContentChip Chip(int i,string name,int max,string description)=>new ContentChip{id=Ids[i],name=name,description=description,rarity=Tier,max=max,icon="hammer",tags=new[]{Tier},excludes=Array.Empty<string>(),mechanic=Mechanics[i],effects=Array.Empty<ChipEffect>()};
        public static ContentChip[] Chips()=>new[]{
            Chip(0,"Bigger Mallet",MalletMax,"Your mallet is 25% bigger: +0.5 m reach and a wider swing, the slam 0.5 m wider, +12% mallet damage."),
            Chip(1,"Hammerspace",1,"Every 4th mallet hit pulls a grand piano out of nowhere. It lands on them 0.55 s later: 30 damage within 2.3 m, flattened 1.2 s."),
            Chip(2,"Anvil Shower",1,"Anvil drops three: two more fall 2.6 m to either side, a beat apart. 20 damage each, flattened 0.8 s."),
            Chip(3,"Banana Bonanza",BananaMax,"+1 peel charge and 2 more peels out. A slip slides them 60% farther, and the peel flies on with them to land again."),
            Chip(4,"Hot Sauce",1,"Mustard Blaster squirts hot sauce: +30% damage, and it sets them burning, 8 a second for 3 s. Its puddles burn too."),
            Chip(5,"Pie Volley",1,"Cream Pie throws three pies in a fan, and the cream stays on 50% longer."),
            Chip(6,"Knockout Glove",1,"The glove reaches 17 m and punches everyone in its line. Punched into a wall, they splat on it: 15 more and stunned 1 s."),
            Chip(7,"Double Boing",1,"Boing! bounces again within 3 s, even in the air. Your landing flattens everyone it catches for 0.6 s, 3.2 m round."),
            Chip(8,"Second Helpings",1,"All You Can Eat fills 35% faster, its lasso 60% wider and 24 m long. The pop throws out 6 hot dogs: walk over one to heal 8."),
            Chip(9,"Toon Logic",1,"Once a round, a killing blow flattens you instead. 1.5 s later you spring up with 35% health, knocking enemies near you back for 20.")};
        /// <summary>Adds the rows to the content (nothing if they are there). For the live content load; the Dev Sandbox's preview calls it on its own copy.</summary>
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(c=>Is(c.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            // The Top Dog fires no projectiles of the ordinary kind: their chips are not for it.
            var blocked=content.chips.Where(c=>c.tags?.Contains("projectile")==true).Select(c=>c.id);
            var row=content.incompatibilities.FirstOrDefault(x=>x.spec==NativeTopDog.Spec);
            if(row==null){row=new ExclusionData{spec=NativeTopDog.Spec,ids=Array.Empty<string>()};content.incompatibilities=content.incompatibilities.Concat(new[]{row}).ToArray();}
            row.ids=row.ids.Concat(blocked).Distinct().ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\ntopdog-chips-rules-v1");
        }
        /// <summary>The line under the card when one goes in.</summary>
        public static string Confirm(string id)
        {
            switch(id){case BiggerMallet:return "BIGGER IS BETTER";case Hammerspace:return "FROM OUT OF NOWHERE";case AnvilShower:return "HEAVY WEATHER";case BananaBonanza:return "WATCH YOUR STEP";
                case HotSauce:return "EXTRA SPICY";case PieVolley:return "DESSERT IS SERVED";case Knockout:return "DOWN FOR THE COUNT";case DoubleBoing:return "BOING BOING";
                case SecondHelpings:return "SECONDS, ANYONE?";case ToonLogic:return "IT'S A CARTOON";default:return "ORDER UP";}
        }

        // ---- the emblems: flat shapes in a 100 box, one per chip ----
        public const float GlyphBox=100;
        private static string F(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        private static string Disc(float x,float y,float r){float k=r*.552f;return "M"+F(x-r)+" "+F(y)+"C"+F(x-r)+" "+F(y-k)+" "+F(x-k)+" "+F(y-r)+" "+F(x)+" "+F(y-r)+"C"+F(x+k)+" "+F(y-r)+" "+F(x+r)+" "+F(y-k)+" "+F(x+r)+" "+F(y)+"C"+F(x+r)+" "+F(y+k)+" "+F(x+k)+" "+F(y+r)+" "+F(x)+" "+F(y+r)+"C"+F(x-k)+" "+F(y+r)+" "+F(x-r)+" "+F(y+k)+" "+F(x-r)+" "+F(y)+"Z";}
        private static string Box(float x0,float y0,float x1,float y1)=>"M"+F(x0)+" "+F(y0)+"L"+F(x1)+" "+F(y0)+"L"+F(x1)+" "+F(y1)+"L"+F(x0)+" "+F(y1)+"Z";
        private static string Pill(float x0,float y0,float x1,float y1){float r=(y1-y0)/2;return "M"+F(x0+r)+" "+F(y0)+"L"+F(x1-r)+" "+F(y0)+"Q"+F(x1)+" "+F(y0)+" "+F(x1)+" "+F(y0+r)+"Q"+F(x1)+" "+F(y1)+" "+F(x1-r)+" "+F(y1)+"L"+F(x0+r)+" "+F(y1)+"Q"+F(x0)+" "+F(y1)+" "+F(x0)+" "+F(y0+r)+"Q"+F(x0)+" "+F(y0)+" "+F(x0+r)+" "+F(y0)+"Z";}
        private static string Star(float x,float y,float r)=>"M"+F(x)+" "+F(y-r)+"L"+F(x+r*.28f)+" "+F(y-r*.28f)+"L"+F(x+r)+" "+F(y)+"L"+F(x+r*.28f)+" "+F(y+r*.28f)+"L"+F(x)+" "+F(y+r)+"L"+F(x-r*.28f)+" "+F(y+r*.28f)+"L"+F(x-r)+" "+F(y)+"L"+F(x-r*.28f)+" "+F(y-r*.28f)+"Z";
        private static string Anvil(float x,float y,float s)=>"M"+F(x-22*s)+" "+F(y-10*s)+"L"+F(x+16*s)+" "+F(y-10*s)+"Q"+F(x+26*s)+" "+F(y-10*s)+" "+F(x+30*s)+" "+F(y-4*s)+"L"+F(x+12*s)+" "+F(y)+"L"+F(x+8*s)+" "+F(y+4*s)+"L"+F(x+14*s)+" "+F(y+12*s)+"L"+F(x-14*s)+" "+F(y+12*s)+"L"+F(x-8*s)+" "+F(y+4*s)+"L"+F(x-12*s)+" "+F(y)+"Q"+F(x-22*s)+" "+F(y-2*s)+" "+F(x-22*s)+" "+F(y-10*s)+"Z";
        private static string Spring(float x,float y0,float y1,float w){string d="";int n=5;float h=(y1-y0)/n;for(int i=0;i<n;i++){float a=y0+i*h;d+="M"+F(x-w)+" "+F(a)+"L"+F(x+w)+" "+F(a+h*.5f)+"L"+F(x+w)+" "+F(a+h*.5f+3)+"L"+F(x-w)+" "+F(a+3)+"Z"+"M"+F(x+w)+" "+F(a+h*.5f)+"L"+F(x-w)+" "+F(a+h)+"L"+F(x-w)+" "+F(a+h+3)+"L"+F(x+w)+" "+F(a+h*.5f+3)+"Z";}return d;}
        private static string Path(string d)=>"<path d=\""+d+"\" fill=\"#fff\"/>";
        private static readonly string[] glyphs=new string[10];
        /// <summary>Chip `id`'s emblem as markup in a <see cref="GlyphBox"/> box, or null when it is not one of the set.</summary>
        public static string Glyph(string id){int i=Index(id);if(i<0)return null;return glyphs[i]??(glyphs[i]=Path(GlyphPath(i)));}
        private static string GlyphPath(int i)
        {
            switch(i)
            {
                // A huge mallet head on a short handle, growing (the arrows off its ends).
                case 0:return Pill(14,12,86,48)+Box(45,48,55,92)+Pill(38,88,62,96)+"M2 30L12 20L12 40Z"+"M98 30L88 20L88 40Z";
                // A grand piano dropping: its lid, its body's curve, its legs, keys.
                case 1:return "M8 34L60 34Q88 34 92 54Q94 66 80 66L8 66Z"+"M10 30L54 6L60 10L18 32Z"+Box(12,66,18,90)+Box(70,66,76,90)+Box(10,58,58,62)+Star(86,14,9);
                // Three anvils, the middle one lower.
                case 2:return Anvil(50,62,1.05f)+Anvil(18,26,.62f)+Anvil(82,26,.62f);
                // Two peels, three lobes each.
                case 3:return "M34 40Q28 70 8 86Q30 80 40 62Q48 84 42 96Q60 76 50 52Q70 70 82 72Q66 54 50 40Z"+Pill(36,30,52,42)+"M76 10Q72 26 62 34Q74 30 80 22Q84 30 82 38Q92 26 84 14Z";
                // A hot sauce bottle and its flame.
                case 4:return Box(42,8,58,22)+"M38 22L62 22L62 30Q76 36 76 54L76 92L24 92L24 54Q24 36 38 30Z"+"M50 46Q60 58 56 70Q62 66 62 58Q70 70 62 82L38 82Q30 70 40 58Q42 66 46 68Q42 56 50 46Z";
                // Three pies in a fan.
                case 5:return "M8 70Q8 58 30 58Q52 58 52 70L48 78L12 78Z"+"M30 38Q30 26 52 26Q74 26 74 38L70 46L34 46Z"+"M52 72Q52 60 74 60Q96 60 96 72L92 80L56 80Z"+Disc(30,56,4)+Disc(52,24,4)+Disc(74,58,4);
                // A boxing glove on its scissor arm, a star where it lands.
                case 6:return "M4 70L18 56L26 64L12 78Z"+"M18 56L32 70L40 62L26 48Z"+"M32 70L46 56L54 64L40 78Z"+"M52 42Q52 22 72 22Q92 22 92 44Q92 64 72 66L58 66Q50 66 50 58Z"+Star(90,84,9);
                // Two springs, the second higher.
                case 7:return Spring(28,46,92,12)+Box(14,40,42,46)+Spring(72,22,72,12)+Box(58,16,86,22)+Star(72,8,7);
                // A plate piled with hot dogs.
                case 8:return "M4 78Q50 98 96 78L92 72Q50 86 8 72Z"+Pill(14,56,86,70)+Pill(22,40,78,54)+Pill(32,24,68,38)+Box(48,4,52,16)+Disc(50,8,4);
                // A pilot flat as a pancake, springing back up.
                default:return "M6 84Q50 74 94 84L94 92Q50 98 6 92Z"+Spring(50,40,74,10)+Disc(50,26,12)+Star(18,24,8)+Star(84,30,7);
            }
        }
    }
}
