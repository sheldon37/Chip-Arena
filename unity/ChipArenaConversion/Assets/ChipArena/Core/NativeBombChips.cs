using System;
using System.Globalization;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Bombadier's own ten chips (owner 2026-11-04: "a set of 10 bombadier chips ... highly likely to show in their chip selection pool; a unique design and an effect when selecting them"; docs/BOMBADIER.md).
    /// A signature set like the Emperor's: their own tier, tag and odds. The rows, names and looks are here; what the mechanics do is the rules' (NativeBombAuthority): a row with a mechanic the authority has not got
    /// yet must stay out of the content, or the native draft would offer a chip that does nothing, so <see cref="Apply"/> is called from the content load once they exist, and the Dev Sandbox previews them meanwhile.
    /// </summary>
    public static class NativeBombChips
    {
        public const string Tier="bombadier",Prefix="bomb-",Color="#ff7a1f";
        public const string ShortFuse="bomb-short-fuse",BlackPowder="bomb-black-powder",Bandolier="bomb-bandolier",BouncingBetty="bomb-bouncing-betty",DaisyChain="bomb-daisy-chain",ScorchedEarth="bomb-scorched-earth",NitroBoots="bomb-nitro-boots",ClusterFun="bomb-cluster-fun",BlastShield="bomb-blast-shield",Encore="bomb-encore";
        public static readonly string[] Ids={ShortFuse,BlackPowder,Bandolier,BouncingBetty,DaisyChain,ScorchedEarth,NitroBoots,ClusterFun,BlastShield,Encore};
        /// <summary>The rule each chip needs beyond the stats in its row (null: its stats are enough).</summary>
        public static readonly string[] Mechanics={ShortFuse,null,null,BouncingBetty,DaisyChain,ScorchedEarth,NitroBoots,ClusterFun,BlastShield,Encore};
        public static bool Is(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal);
        public static int Index(string id)=>Array.IndexOf(Ids,id);
        private static ContentChip Chip(int i,string name,int max,string description,params ChipEffect[] effects)=>new ContentChip{id=Ids[i],name=name,description=description,rarity=Tier,max=max,icon="bomb",tags=new[]{Tier},excludes=Array.Empty<string>(),mechanic=Mechanics[i],effects=effects};
        public static ContentChip[] Chips()=>new[]{
            Chip(0,"Short Fuse",2,"Pop Bombs go off .25 s sooner and fly 15% faster. Less time to get out of the way."),
            Chip(1,"Black Powder",3,"+15% damage on every blast.",NativeMovementContent.Effect("damage","mul",1.15)),
            Chip(2,"Bandolier",3,"+2 sticks on the belt, and restocking takes 20% less time.",NativeMovementContent.Effect("ammoCapacity","add",2),NativeMovementContent.Effect("reloadSpeed","mul",.8)),
            Chip(3,"Bouncing Betty",2,"+1 mine out at once and +1 charge. Mines throw their victim 25% higher."),
            Chip(4,"Daisy Chain",1,"The chain stays open .4 s longer, and links keep more damage: x1, x.9, x.75, x.6."),
            Chip(5,"Scorched Earth",1,"Your blasts leave fire on the floor for 2 s, burning enemies in it for 6 a second."),
            Chip(6,"Nitro Boots",2,"Blast Jump recharges 30% faster and throws you 20% farther, and a firecracker drops where you left."),
            Chip(7,"Cluster Fun",1,"Cluster Lob bursts into 9 firecrackers, not 6, and recharges 20% faster."),
            Chip(8,"Blast Shield",1,"Your own blasts no longer hurt you. They still throw you."),
            Chip(9,"Encore",1,"The Grand Finale's meter fills 25% faster, and 8 more blasts fall with its fireworks, 10 damage each.")};
        /// <summary>Adds the rows to the content (nothing if they are there). For the content load once the rules exist; the Dev Sandbox's preview calls it on its own copy of the session.</summary>
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(c=>Is(c.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nbombadier-chips-v1");
        }
        /// <summary>The line under the card when one goes in.</summary>
        public static string Confirm(string id)
        {
            switch(id){case ShortFuse:return "FUSE CUT SHORT";case BlackPowder:return "BURNS HOTTER";case Bandolier:return "BELT LOADED";case BouncingBetty:return "SPRING WOUND";case DaisyChain:return "CHAIN UNBROKEN";case ScorchedEarth:return "THE FLOOR IS LIT";case NitroBoots:return "FEET FIRST";case ClusterFun:return "MANY HAPPY RETURNS";case BlastShield:return "NOT A SCRATCH";case Encore:return "ONE MORE TIME";default:return "LIVE ORDNANCE";}
        }
        // ---- the emblems: flat shapes in a 100 box, one per chip, drawn in the card's core and large behind its text ----
        public const float GlyphBox=100;
        private static string F(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        private static string Disc(float x,float y,float r){float k=r*.72f,a=r;return "M"+F(x-a)+" "+F(y)+"Q"+F(x-a)+" "+F(y-k)+" "+F(x-k)+" "+F(y-k)+"Q"+F(x-k*.4f)+" "+F(y-a)+" "+F(x)+" "+F(y-a)+"Q"+F(x+k*.4f)+" "+F(y-a)+" "+F(x+k)+" "+F(y-k)+"Q"+F(x+a)+" "+F(y-k)+" "+F(x+a)+" "+F(y)+"Q"+F(x+a)+" "+F(y+k)+" "+F(x+k)+" "+F(y+k)+"Q"+F(x+k*.4f)+" "+F(y+a)+" "+F(x)+" "+F(y+a)+"Q"+F(x-k*.4f)+" "+F(y+a)+" "+F(x-k)+" "+F(y+k)+"Q"+F(x-a)+" "+F(y+k)+" "+F(x-a)+" "+F(y)+"Z";}
        private static string Box(float x0,float y0,float x1,float y1)=>"M"+F(x0)+" "+F(y0)+"L"+F(x1)+" "+F(y0)+"L"+F(x1)+" "+F(y1)+"L"+F(x0)+" "+F(y1)+"Z";
        private static string Star(float x,float y,float r)=>"M"+F(x)+" "+F(y-r)+"L"+F(x+r*.28f)+" "+F(y-r*.28f)+"L"+F(x+r)+" "+F(y)+"L"+F(x+r*.28f)+" "+F(y+r*.28f)+"L"+F(x)+" "+F(y+r)+"L"+F(x-r*.28f)+" "+F(y+r*.28f)+"L"+F(x-r)+" "+F(y)+"L"+F(x-r*.28f)+" "+F(y-r*.28f)+"Z";
        private static string Path(string d)=>"<path d=\""+d+"\" fill=\"#fff\"/>";
        private static readonly string[] glyphs=new string[10];
        /// <summary>Chip `id`'s emblem as markup in a <see cref="GlyphBox"/> box, or null when it is not one of the set.</summary>
        public static string Glyph(string id){int i=Index(id);if(i<0)return null;return glyphs[i]??(glyphs[i]=Path(GlyphPath(i)));}
        private static string GlyphPath(int i)
        {
            switch(i)
            {
                // A stick with almost no fuse left, the spark nearly on it.
                case 0:return "M14 74L44 44L54 54L24 84Z"+"M49 39L60 28L70 38L59 49Z"+"M63 26L70 19L74 23L67 30Z"+Star(82,14,13);
                // A keg: its hoops, a stub of fuse.
                case 1:return "M28 28Q50 22 72 28L72 34Q50 28 28 34Z"+"M26 40Q50 34 74 40Q78 56 74 70Q50 64 26 70Q22 56 26 40Z"+"M28 76Q50 70 72 76L72 82Q50 88 28 82Z"+Box(47,8,53,24)+Star(50,8,8);
                // Five sticks on a belt.
                case 2:return Box(10,24,23,62)+Box(29,18,42,56)+Box(48,12,61,50)+Box(67,18,80,56)+"M6 74L94 62L94 72L6 84Z"+Box(15,14,18,24)+Box(34,8,37,18)+Box(53,3,56,12)+Box(72,8,75,18);
                // A mine, its spring, the bounce above it.
                case 3:return "M12 56Q12 42 50 42Q88 42 88 56Q88 66 50 66Q12 66 12 56Z"+"M38 41Q50 24 62 41Z"+"M40 70L60 70L60 75L44 75L44 78L60 78L60 83L44 83L44 86L60 86L60 92L40 92L40 87L56 87L56 84L40 84L40 79L56 79L56 76L40 76Z"+"M50 4L66 19L59 19L50 11L41 19L34 19Z";
                // Three blasts, handing on.
                case 4:return Disc(18,76,14)+Disc(50,46,17)+Disc(84,18,12)+"M26 62L37 52L41 57L31 67Z"+"M62 32L74 22L78 27L67 37Z"+Star(18,76,0)+"";
                // Flame on the floor.
                case 5:return "M50 4Q72 32 66 52Q78 44 76 28Q94 52 80 74L20 74Q6 52 22 34Q24 46 35 50Q28 26 50 4Z"+Box(8,80,92,88)+Box(20,92,80,96);
                // A boot on three jets.
                case 6:return "M26 6L56 6L56 40L84 52Q92 56 92 68L26 68Z"+"M30 72L44 72L37 98Z"+"M50 72L64 72L57 92Z"+"M70 72L84 72L77 86Z";
                // The shell, and what comes out of it.
                case 7:return Disc(50,32,24)+Disc(12,66,8)+Disc(30,80,8)+Disc(50,88,8)+Disc(70,80,8)+Disc(88,66,8)+Box(47,3,53,9);
                // A shield, the blast breaking on it.
                case 8:return "M50 16L82 26L82 54Q80 76 50 92Q20 76 18 54L18 26Z"+Star(12,10,10)+Star(88,10,10)+Star(95,60,6)+Star(5,60,6);
                // The plunger and its crate, and the stars for it.
                default:return Box(22,10,78,20)+Box(45,20,55,48)+Box(22,52,78,92)+Star(10,36,9)+Star(90,36,9);
            }
        }
    }
}
