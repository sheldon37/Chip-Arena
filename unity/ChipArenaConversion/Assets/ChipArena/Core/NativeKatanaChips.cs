using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Katana's own ten chips (owner 2026-11-10: "Both with unique chips with their abilities that play unique animations when selected";
    /// docs/KATANA.md "The chips"). A signature set like the Glaive's: its own tier, tag and odds, offered only to the Katana. Runtime
    /// NativeKatanaAuthority implements the mechanics (Claude builds this class's rules at the owner's word, logged for Astra), Core
    /// NativeBladeFx draws them in the world, Runtime NativeBladeChipArt the card and the pick. Apply installs the rows over the live content.
    /// </summary>
    public static class NativeKatanaChips
    {
        public const string Tier="katana",Prefix="katana-",Color="#ff3d6e",Label="KATANA SPECIAL";
        public const string HonedEdge="katana-honed-edge",CrescentWave="katana-crescent-wave",ThousandPetals="katana-thousand-petals",TwinMoons="katana-twin-moons",SeveringLine="katana-severing-line",
            FlashStep="katana-flash-step",StillWater="katana-still-water",MirrorMoon="katana-mirror-moon",FallingBlossom="katana-falling-blossom",SheathedFury="katana-sheathed-fury";
        public static readonly string[] Ids={HonedEdge,CrescentWave,ThousandPetals,TwinMoons,SeveringLine,FlashStep,StillWater,MirrorMoon,FallingBlossom,SheathedFury};
        public static bool Is(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal)&&Array.IndexOf(Ids,id)>=0;
        public static int Index(string id)=>Array.IndexOf(Ids,id);
        /// <summary>The record's bit for a chip held (NativeBladeKit.Chips), for the look.</summary>
        public static int Bit(string id){int i=Index(id);return i<0?0:1<<i;}
        public static bool Holds(NativeBladeKit k,string id)=>k!=null&&(k.Chips&Bit(id))!=0;
        /// <summary>How long the draft holds on a pick of one of these (s): the plant, then its animation (Runtime NativeBladeChipArt.Reveal).</summary>
        public const double RevealSeconds=5.2;

        // ---- the numbers (the rules', the effects' and the cards') ----
        /// <summary>Honed Edge, a copy: more Katana damage; every cut's bleed (a second, seconds), refreshed by the next; how often a drip shows.</summary>
        public const int HonedMax=2;public const float HonedDamage=.1f,BleedDps=3,BleedFor=2,BleedShow=.5f;
        /// <summary>Crescent Wave: the finisher's wave of light, how far (m), how fast, how wide each side (m), its cut.</summary>
        public const float WaveReach=8,WaveSpeed=22,WaveWidth=.7f,WaveDamage=16;
        /// <summary>Thousand Petals: more storm cuts, and each draws foes in (m, never closer than PetalClose).</summary>
        public const int PetalsMore=4;public const float PetalPull=1,PetalClose=1.1f;
        /// <summary>Twin Moons: the second split's delay after the first and its share of the iai's damage.</summary>
        public const float TwinDelay=.3f,TwinShare=.6f;
        /// <summary>Severing Line: the split kills a foe it leaves below this share of health; a kill takes this much off Q.</summary>
        public const float SeverBelow=.2f,SeverRefund=4;
        /// <summary>Flash Step: the rush this much farther, untouchable through its dash.</summary>
        public const float FlashFarther=1.6f;
        /// <summary>Still Water: after this long without attacking, the next cut deals this much more.</summary>
        public const float StillAfter=1.2f,StillDamage=1.5f;
        /// <summary>Mirror Moon: the counter's window this much longer, its damage this much more, the health it gives back.</summary>
        public const float MirrorWindow=2,MirrorDamage=1.5f,MirrorHeal=15;
        /// <summary>Falling Blossom: the storm's launch higher (m/s); a launched foe takes this much more from your next cut within BlossomFor s.</summary>
        public const float BlossomLift=9.5f,BlossomDamage=1.6f,BlossomFor=2;
        /// <summary>Sheathed Fury: Q's cooldown share and the iai's draw (s).</summary>
        public const float FuryCooldown=.7f,FuryDraw=.2f;

        public static readonly string[] Mechanics=Ids;
        private static ContentChip Chip(int i,string name,int max,string description)=>new ContentChip{id=Ids[i],name=name,description=description,rarity=Tier,max=max,icon="sword",tags=new[]{Tier},excludes=Array.Empty<string>(),mechanic=Mechanics[i],effects=Array.Empty<ChipEffect>()};
        public static ContentChip[] Chips()=>new[]{
            Chip(0,"Honed Edge",HonedMax,"+10% Katana damage a copy (cuts, rush, iai, storm, counter). Every cut makes them bleed 3 a second for 2 s, refreshed by the next."),
            Chip(1,"Crescent Wave",1,"The fourth cut sends a crescent of light 8 m ahead, cutting everyone it passes for 16."),
            Chip(2,"Thousand Petals",1,"Petal Storm cuts 4 more times (10 in all), and every cut draws enemies 1 m in."),
            Chip(3,"Twin Moons",1,"Moonlit Draw's line splits twice: a second split 0.3 s after the first, for 60% of its damage."),
            Chip(4,"Severing Line",1,"Moonlit Draw's split kills anyone it leaves below 20% health. A kill takes 4 s off its cooldown."),
            Chip(5,"Flash Step",1,"Rushing Draw dashes 60% farther, and you are untouchable while it dashes."),
            Chip(6,"Still Water",1,"After 1.2 s without attacking, your next cut deals +50% damage."),
            Chip(7,"Mirror Moon",1,"The counter's window after a block or parry lasts 2 s. The counter deals +50% and heals you for 15."),
            Chip(8,"Falling Blossom",1,"Petal Storm's last cut throws them higher, and your next cut on a launched enemy within 2 s deals +60%."),
            Chip(9,"Sheathed Fury",1,"Moonlit Draw's cooldown is 30% shorter and its draw is quicker (0.2 s).")};
        /// <summary>Adds the rows to the content (nothing if they are there). For the live content load; the Dev Sandbox's preview calls it on its own copy.</summary>
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(c=>Is(c.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            // The Katana throws nothing of the ordinary kind: the projectile chips are not for it.
            var blocked=content.chips.Where(c=>c.tags?.Contains("projectile")==true).Select(c=>c.id);
            var row=content.incompatibilities.FirstOrDefault(x=>x.spec==NativeKatana.Spec);
            if(row==null){row=new ExclusionData{spec=NativeKatana.Spec,ids=Array.Empty<string>()};content.incompatibilities=content.incompatibilities.Concat(new[]{row}).ToArray();}
            row.ids=row.ids.Concat(blocked).Distinct().ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nkatana-chips-rules-v1");
        }
        /// <summary>The line under the card when one goes in.</summary>
        public static string Confirm(string id)
        {
            switch(id){case HonedEdge:return "RAZOR'S EDGE";case CrescentWave:return "THE MOON CUTS";case ThousandPetals:return "PETALS FALL";case TwinMoons:return "TWICE CUT";
                case SeveringLine:return "THE LINE HOLDS";case FlashStep:return "GONE IN A FLASH";case StillWater:return "STILL WATER RUNS DEEP";case MirrorMoon:return "AS ABOVE, SO BELOW";
                case FallingBlossom:return "UP YOU GO";case SheathedFury:return "QUICK DRAW";default:return "BLADE DRAWN";}
        }
        /// <summary>The crescent wave's front at `age` s after it set off from `from` along `dir` (flat): how far it has gone.</summary>
        public static float WaveTravel(float age)=>Math.Max(0,Math.Min(WaveReach,WaveSpeed*age));

        // ---- the pick's cues (Runtime NativeBladeChipArt.Reveal, NativeBladePresentation's BladePick) ----
        public static readonly NativeTopDogSounds.IntroCue[][] Picks=BuildPicks();
        private static NativeTopDogSounds.IntroCue[][] BuildPicks()
        {
            NativeTopDogSounds.IntroCue C(float at,string id,float gain,float rate=1)=>new NativeTopDogSounds.IntroCue(at,id,gain,rate);
            var l=new[]{
                // Honed Edge: the whetstone's two strokes, the ring of the edge, the glint.
                new[]{C(.05f,"blade-hone",.7f),C(.4f,"blade-hone",.7f,1.1f),C(.9f,"blade-ring",.8f),C(1.2f,"blade-chime",.5f,1.3f)},
                // Crescent Wave: the draw, the cut, the wave's rush out, its fading hum.
                new[]{C(0,"blade-draw",.7f),C(.34f,"blade-cut",1),C(.4f,"blade-wave",.9f),C(1.1f,"blade-chime",.5f,.9f)},
                // Thousand Petals: the wind rising, the flurry of cuts, the last.
                new[]{C(0,"blade-wind",.6f),C(.3f,"blade-cut",.6f,1.3f),C(.42f,"blade-cut",.6f,1.4f),C(.54f,"blade-cut",.6f,1.5f),C(.66f,"blade-cut",.6f,1.6f),C(.9f,"blade-cut",.9f,1),C(1.3f,"blade-chime",.6f)},
                // Twin Moons: the draw, two splits, two clicks.
                new[]{C(0,"blade-draw",.8f),C(.3f,"blade-dash",.8f),C(.8f,"blade-split",.9f),C(1.12f,"blade-split",.8f,1.2f),C(1.4f,"blade-click",.8f)},
                // Severing Line: the line drawn, the long hush, the split, the click.
                new[]{C(0,"blade-dash",.8f,.9f),C(.25f,"blade-wind",.4f,.7f),C(1,"blade-split",1),C(1.05f,"blade-ring",.6f,.8f),C(1.3f,"blade-click",.9f)},
                // Flash Step: the vanish, the rush, the flash.
                new[]{C(0,"blade-dash",.7f,1.3f),C(.12f,"blade-cut",.8f,1.2f),C(.5f,"blade-chime",.6f,1.4f)},
                // Still Water: a drop, the ripple, the stillness, the cut.
                new[]{C(.1f,"blade-drop",.8f),C(.5f,"blade-chime",.4f,.8f),C(1.4f,"blade-cut",.9f,.8f),C(1.45f,"blade-ring",.6f)},
                // Mirror Moon: the clash, the turn, the counter.
                new[]{C(.1f,"blade-clash",1),C(.6f,"blade-wind",.4f,1.2f),C(.9f,"blade-cut",1,.9f),C(1.2f,"blade-chime",.6f)},
                // Falling Blossom: the rising cut, the blossom's fall, the strike.
                new[]{C(0,"blade-cut",.8f,.8f),C(.2f,"blade-wind",.5f,1.3f),C(1.1f,"blade-cut",.9f,1.2f),C(1.15f,"blade-chime",.6f,1.1f)},
                // Sheathed Fury: the blade slid home, the click, the flash of the draw.
                new[]{C(0,"blade-sheathe",.8f),C(.35f,"blade-click",1),C(.75f,"blade-draw",.9f,1.3f),C(.8f,"blade-ring",.6f,1.2f)},
            };
            foreach(var a in l)Array.Sort(a,(x,y)=>x.At.CompareTo(y.At));return l;
        }

        // ---- the emblems: flat shapes in a 100 box, one per chip ----
        public const float GlyphBox=100;
        private static readonly string[] glyphs=new string[10];
        /// <summary>Chip `id`'s emblem as markup in a <see cref="GlyphBox"/> box, or null when it is not one of the set.</summary>
        public static string Glyph(string id){int i=Index(id);if(i<0)return null;return glyphs[i]??(glyphs[i]=BladeGlyph.Path(GlyphPath(i)));}
        private static string GlyphPath(int i)
        {
            var g=new BladeGlyph();
            switch(i)
            {
                // A blade drawn across a whetstone, a glint at its edge.
                case 0:return g.Katana(14,80,86,20,6)+g.Seg(18,90,64,90,8)+g.Spark(72,26,12);
                // A crescent of light flying off a blade.
                case 1:return g.Katana(10,88,46,52,4.5f)+g.Arc(66,50,26,9,-70,70,true,24)+g.Arc(80,50,18,5,-60,60,true,18);
                // Petals whirling round a centre.
                case 2:{string d="";for(int k=0;k<6;k++)d+=g.Petal(50+30*(float)Math.Cos(k*1.047),50+30*(float)Math.Sin(k*1.047),11,k*60+40);return d+g.Disc(50,50,6);}
                // Two moons, one above the other, a line between.
                case 3:return g.Moon(30,30,16,.55f)+g.Moon(70,70,16,.55f)+g.Seg(14,86,86,14,3);
                // A line through a figure, the halves sliding apart.
                case 4:return g.Seg(10,90,90,10,4)+g.Poly(new List<Vector2>{new Vector2(34,24),new Vector2(58,24),new Vector2(40,60),new Vector2(28,56)})+g.Poly(new List<Vector2>{new Vector2(46,66),new Vector2(70,40),new Vector2(72,76),new Vector2(48,80)});
                // A blade leaving afterimages.
                case 5:return g.Katana(46,74,92,28,5)+g.Seg(8,60,38,60,4)+g.Seg(4,72,34,72,4)+g.Seg(12,84,40,84,4);
                // A drop over ripples.
                case 6:return g.Drop(50,30,12)+g.Arc(50,74,34,4,190,350,false,20)+g.Arc(50,74,22,4,195,345,false,16)+g.Arc(50,74,10,4,200,340,false,12);
                // Two blades meeting, a moon behind.
                case 7:return g.Moon(50,46,30,.5f)+g.Katana(12,84,70,30,4)+g.Katana(88,84,30,30,4);
                // A blossom falling, a rising arc beneath.
                case 8:return g.Petal(50,30,16,0)+g.Petal(36,44,10,-30)+g.Petal(64,46,10,30)+g.Arc(50,96,40,7,-150,-30,true,24);
                // A blade going into its scabbard.
                default:return g.Seg(16,84,72,28,12)+g.Katana(40,60,92,8,4)+g.Spark(76,24,9);
            }
        }
    }

    /// <summary>The blade classes' emblem drawing kit (flat shapes in a 100 box, polygons each turned the same way, holes the other), shared by
    /// the Katana's and the Fangs' chip emblems.</summary>
    internal sealed class BladeGlyph
    {
        private static string F(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        public string Poly(List<Vector2> p,bool hole=false)
        {
            float area=0;for(int i=0,j=p.Count-1;i<p.Count;j=i,i++)area+=(p[j].X*p[i].Y-p[i].X*p[j].Y);
            if(area<0!=hole)p.Reverse();var s=new System.Text.StringBuilder();
            for(int i=0;i<p.Count;i++)s.Append(i==0?"M":"L").Append(F(p[i].X)).Append(' ').Append(F(p[i].Y));return s.Append('Z').ToString();
        }
        public static string Path(string d)=>"<path d=\""+d+"\" fill=\"#fff\"/>";
        public List<Vector2> Circle(float x,float y,float r,int n=28){var l=new List<Vector2>(n);for(int i=0;i<n;i++){double a=i*2*Math.PI/n;l.Add(new Vector2(x+r*(float)Math.Cos(a),y+r*(float)Math.Sin(a)));}return l;}
        public string Disc(float x,float y,float r)=>Poly(Circle(x,y,r));
        public string Seg(float x0,float y0,float x1,float y1,float w)
        {
            var d=new Vector2(x1-x0,y1-y0);float l=d.Length();if(l<1e-4f)return "";var n=new Vector2(-d.Y,d.X)/l*(w/2);
            return Poly(new List<Vector2>{new Vector2(x0,y0)+n,new Vector2(x1,y1)+n,new Vector2(x1,y1)-n,new Vector2(x0,y0)-n});
        }
        /// <summary>A band along a circle's arc (degrees, y down), `w` wide; `taper` thins both ends to points.</summary>
        public string Arc(float x,float y,float r,float w,float from,float to,bool taper=false,int n=20)
        {
            var o=new List<Vector2>();var i=new List<Vector2>();
            for(int k=0;k<=n;k++){float u=k/(float)n;double a=(from+(to-from)*u)*Math.PI/180;float half=w/2*(taper?(float)Math.Sin(Math.PI*u):1);var d=new Vector2((float)Math.Cos(a),(float)Math.Sin(a));o.Add(new Vector2(x,y)+d*(r+half));i.Add(new Vector2(x,y)+d*(r-half));}
            i.Reverse();o.AddRange(i);return Poly(o);
        }
        public string Spark(float x,float y,float r)=>Poly(new List<Vector2>{new Vector2(x,y-r),new Vector2(x+r*.2f,y-r*.2f),new Vector2(x+r,y),new Vector2(x+r*.2f,y+r*.2f),new Vector2(x,y+r),new Vector2(x-r*.2f,y+r*.2f),new Vector2(x-r,y),new Vector2(x-r*.2f,y-r*.2f)});
        /// <summary>A katana from its pommel (x0, y0) to its tip (x1, y1), blade `w` wide: a gentle curve, a guard and a grip.</summary>
        public string Katana(float x0,float y0,float x1,float y1,float w)
        {
            var d=new Vector2(x1-x0,y1-y0);float l=d.Length();if(l<1e-3f)return "";d/=l;var n=new Vector2(-d.Y,d.X);var a=new Vector2(x0,y0);var g=a+d*(l*.26f);
            var blade=new List<Vector2>();var back=new List<Vector2>();
            for(int k=0;k<=12;k++){float u=k/12f;float bow=(float)Math.Sin(Math.PI*u*.9f)*l*.05f;var c=g+d*((l*.74f)*u)+n*bow;float half=w*.5f*(u<.88f?1:(1-(u-.88f)/.12f));blade.Add(c+n*half);back.Add(c-n*half);}
            back.Reverse();blade.AddRange(back);
            return Poly(blade)+Seg(g.X-n.X*w*1.3f,g.Y-n.Y*w*1.3f,g.X+n.X*w*1.3f,g.Y+n.Y*w*1.3f,w*.8f)+Seg(a.X,a.Y,g.X,g.Y,w*.9f);
        }
        /// <summary>A petal at (x, y), `r` long, pointing along `deg`.</summary>
        public string Petal(float x,float y,float r,float deg)
        {
            double a=deg*Math.PI/180;var d=new Vector2((float)Math.Cos(a),(float)Math.Sin(a));var n=new Vector2(-d.Y,d.X);var c=new Vector2(x,y);var l=new List<Vector2>();
            for(int k=0;k<=10;k++){float u=k/10f;l.Add(c+d*(r*(u*2-1))+n*(r*.45f*(float)Math.Sin(Math.PI*u)));}
            for(int k=9;k>=1;k--){float u=k/10f;l.Add(c+d*(r*(u*2-1))-n*(r*.45f*(float)Math.Sin(Math.PI*u)*(u>.8f?.4f:1)));}
            return Poly(l);
        }
        /// <summary>A crescent moon: a disc less an offset disc (`cut` of its radius across).</summary>
        public string Moon(float x,float y,float r,float cut)
        {
            var o=new List<Vector2>();var i=new List<Vector2>();
            for(int k=0;k<=24;k++){double a=Math.PI*.5+Math.PI*k/24;o.Add(new Vector2(x+r*(float)Math.Cos(a)*-1,y+r*(float)Math.Sin(a)));}
            for(int k=24;k>=0;k--){double a=Math.PI*.5+Math.PI*k/24;i.Add(new Vector2(x+r*(float)Math.Cos(a)*-(1-2*cut)+r*cut*.2f,y+r*(float)Math.Sin(a)*.92f));}
            o.AddRange(i);return Poly(o);
        }
        /// <summary>A drop at (x, y), `r` wide, its point up.</summary>
        public string Drop(float x,float y,float r){var l=new List<Vector2>();for(int k=0;k<=20;k++){double a=Math.PI*k/20;l.Add(new Vector2(x+r*(float)Math.Cos(a),y+r*.4f+r*(float)Math.Sin(a)));}l.Add(new Vector2(x,y-r*1.4f));return Poly(l);}
        /// <summary>A fang from its root (x0, y0) to its point (x1, y1), `w` wide at the root, curving to its left.</summary>
        public string Fang(float x0,float y0,float x1,float y1,float w)
        {
            var d=new Vector2(x1-x0,y1-y0);float l=d.Length();if(l<1e-3f)return "";d/=l;var n=new Vector2(-d.Y,d.X);var a=new Vector2(x0,y0);var o=new List<Vector2>();var b=new List<Vector2>();
            for(int k=0;k<=12;k++){float u=k/12f;var c=a+d*(l*u)+n*(l*.16f*u*u);float half=w*.5f*(1-u);o.Add(c+n*half);b.Add(c-n*half);}
            b.Reverse();o.AddRange(b);return Poly(o);
        }
    }
}
