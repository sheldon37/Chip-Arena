using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Fangs' own ten chips (owner 2026-11-10: "Both with unique chips with their abilities that play unique animations when selected";
    /// docs/FANGS.md "The chips"). Their own tier, tag and odds, offered only to the Fangs (a DEV class). Runtime NativeFangsAuthority implements
    /// the mechanics, Core NativeBladeFx draws them in the world, Runtime NativeBladeChipArt the card and the pick.
    /// </summary>
    public static class NativeFangsChips
    {
        public const string Tier="fangs",Prefix="fangs-",Color="#7dff3a",Label="FANGS SPECIAL";
        public const string Neurotoxin="fangs-neurotoxin",Hydra="fangs-hydra",LongShadow="fangs-long-shadow",SilentVeil="fangs-silent-veil",ChainBloom="fangs-chain-bloom",
            Bloodletting="fangs-bloodletting",Predator="fangs-predator",DeathMark="fangs-death-mark",Smokescreen="fangs-smokescreen",TwinVenom="fangs-twin-venom";
        public static readonly string[] Ids={Neurotoxin,Hydra,LongShadow,SilentVeil,ChainBloom,Bloodletting,Predator,DeathMark,Smokescreen,TwinVenom};
        public static bool Is(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal)&&Array.IndexOf(Ids,id)>=0;
        public static int Index(string id)=>Array.IndexOf(Ids,id);
        public static int Bit(string id){int i=Index(id);return i<0?0:1<<i;}
        public static bool Holds(NativeBladeKit k,string id)=>k!=null&&(k.Chips&Bit(id))!=0;
        public const double RevealSeconds=5.2;

        // ---- the numbers ----
        /// <summary>Neurotoxin, a copy: venom this much stronger; each stack slows by NeuroSlow (at most NeuroSlowMost).</summary>
        public const int NeuroMax=2;public const float NeuroDamage=.3f,NeuroSlow=.04f,NeuroSlowMost=.24f;
        /// <summary>Hydra: more knives, and each passes through its first foe.</summary>
        public const int HydraMore=4;
        /// <summary>Long Shadow: the step reaches farther (m), with two charges.</summary>
        public const float ShadowFarther=6;public const int ShadowCharges=2;
        /// <summary>Silent Veil: the veil this much longer (s); the first attack from it keeps it.</summary>
        public const float SilentLonger=2;
        /// <summary>Chain Bloom: a bloom's kill leaps its stacks to everyone within ChainRange m.</summary>
        public const float ChainRange=5;
        /// <summary>Bloodletting: the share of your venom's damage you heal.</summary>
        public const float DrainShare=.4f;
        /// <summary>Predator: more damage to foes below PreyBelow of their health; more move speed.</summary>
        public const float PreyBelow=.5f,PreyDamage=1.25f,PreySpeed=1.12f;
        /// <summary>Death Mark: the assassination's health threshold; a takedown kill resets its cooldown.</summary>
        public const float MarkThreshold=.6f;
        /// <summary>Smokescreen: the veil's smoke cloud: its radius (m), how long (s), the share of damage foes inside it deal.</summary>
        public const float CloudRadius=2.25f,CloudFor=5,CloudShare=.7f;
        /// <summary>Twin Venom: the most venom stacks, and the finisher's.</summary>
        public const int TwinMax=10,TwinFinisher=3;

        public static readonly string[] Mechanics=Ids;
        private static ContentChip Chip(int i,string name,int max,string description)=>new ContentChip{id=Ids[i],name=name,description=description,rarity=Tier,max=max,icon="swords",tags=new[]{Tier},excludes=Array.Empty<string>(),mechanic=Mechanics[i],effects=Array.Empty<ChipEffect>()};
        public static ContentChip[] Chips()=>new[]{
            Chip(0,"Neurotoxin",NeuroMax,"Venom deals +30% a copy, and each stack slows its victim by 4% (24% at most)."),
            Chip(1,"Hydra",1,"Fang Fan throws 4 more knives (9), and each one passes through its first enemy."),
            Chip(2,"Long Shadow",1,"Shadow Step reaches 6 m farther (18 m) and holds two charges."),
            Chip(3,"Silent Veil",1,"Smoke Veil lasts 2 s longer, and your first attack from it does not end it."),
            Chip(4,"Chain Bloom",1,"When Venom Bloom kills, the victim's venom leaps to every enemy within 5 m."),
            Chip(5,"Bloodletting",1,"Your venom heals you for 40% of the damage it deals."),
            Chip(6,"Predator",1,"+25% damage to enemies below half health, and +12% move speed."),
            Chip(7,"Death Mark",1,"Assassinate takes down enemies below 60% health, and a takedown kill resets its cooldown."),
            Chip(8,"Smokescreen",1,"Smoke Veil leaves a cloud 4.5 m across for 5 s. Enemies inside deal 30% less damage."),
            Chip(9,"Twin Venom",1,"Venom stacks to 10, and the flurry's last cut leaves 3 stacks.")};
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(c=>Is(c.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            // The Fangs' knives are their own: the ordinary projectile chips are not for them.
            var blocked=content.chips.Where(c=>c.tags?.Contains("projectile")==true).Select(c=>c.id);
            var row=content.incompatibilities.FirstOrDefault(x=>x.spec==NativeFangs.Spec);
            if(row==null){row=new ExclusionData{spec=NativeFangs.Spec,ids=Array.Empty<string>()};content.incompatibilities=content.incompatibilities.Concat(new[]{row}).ToArray();}
            row.ids=row.ids.Concat(blocked).Distinct().ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nfangs-chips-rules-v1");
        }
        public static string Confirm(string id)
        {
            switch(id){case Neurotoxin:return "SLOW POISON";case Hydra:return "CUT ONE, TWO MORE";case LongShadow:return "LONG REACH";case SilentVeil:return "NOT A SOUND";
                case ChainBloom:return "IT SPREADS";case Bloodletting:return "DRINK DEEP";case Predator:return "SMELL THE FEAR";case DeathMark:return "MARKED FOR DEATH";
                case Smokescreen:return "NOWHERE TO HIDE";case TwinVenom:return "DOUBLE DOSE";default:return "FANGS BARED";}
        }

        public static readonly NativeTopDogSounds.IntroCue[][] Picks=BuildPicks();
        private static NativeTopDogSounds.IntroCue[][] BuildPicks()
        {
            NativeTopDogSounds.IntroCue C(float at,string id,float gain,float rate=1)=>new NativeTopDogSounds.IntroCue(at,id,gain,rate);
            var l=new[]{
                // Neurotoxin: drips into the vial, a bubbling, a hiss.
                new[]{C(.1f,"fang-drip",.7f),C(.4f,"fang-drip",.7f,1.2f),C(.7f,"fang-drip",.7f,1.4f),C(1,"fang-hiss",.8f),C(1.4f,"blade-chime",.4f,.8f)},
                // Hydra: one knife thrown, three heads splitting off it.
                new[]{C(0,"fang-throw",.8f),C(.45f,"fang-throw",.6f,1.2f),C(.5f,"fang-throw",.6f,1.35f),C(.55f,"fang-throw",.6f,1.5f),C(1,"fang-hiss",.6f,1.2f)},
                // Long Shadow: the shadow stretching, two vanishes.
                new[]{C(0,"fang-vanish",.6f,.8f),C(.6f,"fang-vanish",.8f),C(1,"fang-vanish",.8f,1.2f),C(1.1f,"blade-cut",.7f,1.3f)},
                // Silent Veil: the smoke, a hush, nothing.
                new[]{C(0,"fang-smoke",.9f),C(.6f,"blade-wind",.3f,.6f),C(1.5f,"blade-chime",.3f,.7f)},
                // Chain Bloom: one burst, the venom leaping, more bursts.
                new[]{C(.1f,"fang-bloom",.9f),C(.5f,"fang-hiss",.5f,1.3f),C(.7f,"fang-bloom",.7f,1.2f),C(.82f,"fang-bloom",.7f,1.35f),C(.94f,"fang-bloom",.7f,1.5f)},
                // Bloodletting: drips, a heartbeat, the drink.
                new[]{C(.1f,"fang-drip",.7f,.8f),C(.5f,"fang-beat",.9f),C(.95f,"fang-beat",.9f),C(1.3f,"blade-chime",.5f,1.1f)},
                // Predator: a low growl of wind, the eyes open, the strike.
                new[]{C(0,"blade-wind",.5f,.5f),C(.6f,"blade-chime",.6f,.6f),C(1.1f,"blade-cut",1,1.1f)},
                // Death Mark: the mark burned in, the toll.
                new[]{C(0,"fang-hiss",.6f,.7f),C(.5f,"fang-toll",.9f),C(1.3f,"fang-toll",.6f,.8f)},
                // Smokescreen: the bomb, the billow.
                new[]{C(0,"fang-throw",.6f,.8f),C(.3f,"fang-smoke",1),C(.9f,"fang-smoke",.6f,.8f)},
                // Twin Venom: two fangs ringing, two drips.
                new[]{C(.05f,"blade-ring",.7f,1.2f),C(.25f,"blade-ring",.7f,1.4f),C(.8f,"fang-drip",.8f),C(.95f,"fang-drip",.8f,1.2f),C(1.3f,"fang-hiss",.6f)},
            };
            foreach(var a in l)Array.Sort(a,(x,y)=>x.At.CompareTo(y.At));return l;
        }

        public const float GlyphBox=100;
        private static readonly string[] glyphs=new string[10];
        public static string Glyph(string id){int i=Index(id);if(i<0)return null;return glyphs[i]??(glyphs[i]=BladeGlyph.Path(GlyphPath(i)));}
        private static string GlyphPath(int i)
        {
            var g=new BladeGlyph();
            switch(i)
            {
                // A fang dripping into a bubbling drop.
                case 0:return g.Fang(30,10,46,58,16)+g.Drop(54,76,9)+g.Disc(76,70,4)+g.Disc(30,80,3);
                // Three fang knives fanned from one.
                case 1:return g.Fang(50,90,50,40,10)+g.Fang(42,62,14,20,8)+g.Fang(58,62,86,20,8)+g.Fang(50,50,50,8,7);
                // A long shadow with two figures' marks.
                case 2:return g.Seg(10,80,90,80,6)+g.Poly(new List<Vector2>{new Vector2(18,76),new Vector2(52,30),new Vector2(62,34),new Vector2(32,76)})+g.Disc(76,40,7)+g.Disc(58,22,5);
                // A swirl of smoke, hushed.
                case 3:return g.Arc(50,50,34,8,-30,240,true,26)+g.Arc(50,50,20,7,120,390,true,22)+g.Seg(30,50,70,50,5);
                // A burst leaping to two more.
                case 4:return g.Spark(28,68,20)+g.Spark(70,30,13)+g.Spark(80,74,11)+g.Seg(36,60,62,36,3)+g.Seg(40,70,70,74,3);
                // A fang over a drop in a heart.
                case 5:return g.Drop(50,62,22)+g.Fang(40,6,56,44,12);
                // Two eyes in the dark, a fang below.
                case 6:return g.Petal(32,36,14,0)+g.Petal(68,36,14,180)+g.Fang(38,64,62,92,12);
                // A skull's crosshair: a ring, a cross, a mark.
                case 7:return g.Arc(50,50,34,6,0,360,false,36)+g.Seg(50,4,50,30,5)+g.Seg(50,70,50,96,5)+g.Seg(4,50,30,50,5)+g.Seg(70,50,96,50,5)+g.Disc(50,50,8);
                // A smoke bomb and its billow.
                case 8:return g.Disc(30,70,14)+g.Seg(36,58,46,46,4)+g.Arc(66,40,22,9,160,400,true,26)+g.Arc(70,38,10,7,180,420,true,18);
                // Two fangs crossed, two drops.
                default:return g.Fang(16,16,80,84,12)+g.Fang(84,16,20,84,12)+g.Drop(22,84,6)+g.Drop(78,84,6);
            }
        }
    }
}
