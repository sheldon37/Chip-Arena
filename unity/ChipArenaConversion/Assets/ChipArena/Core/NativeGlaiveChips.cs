using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Glaive's own ten chips (owner 2026-11-09: "create uniquely designed chips to benefit the glaive with effects on selection";
    /// docs/GLAIVE.md "The chips"). A signature set like the Top Dog's: its own tier, tag and odds, offered only to the Glaive. Runtime
    /// NativeGlaiveAuthority implements the mechanics (the owner chose that Claude builds this class's rules), Core NativeGlaiveFx draws them
    /// in the world, Runtime NativeGlaiveChipArt the card and the pick. Apply installs the rows over the live content, as NativeDogChips does.
    /// </summary>
    public static class NativeGlaiveChips
    {
        public const string Tier="glaive",Prefix="glaive-",Color="#4fe8ff";
        public const string FourthBounce="glaive-fourth-bounce",KeenEdge="glaive-keen-edge",TwinOrbit="glaive-twin-orbit",Shatterpoint="glaive-shatterpoint",MagnetReturn="glaive-magnet-return",
            QuickCatch="glaive-quick-catch",Whirlwind="glaive-whirlwind",LongArc="glaive-long-arc",SkimmingStone="glaive-skimming-stone",CrescentGuard="glaive-crescent-guard";
        public static readonly string[] Ids={FourthBounce,KeenEdge,TwinOrbit,Shatterpoint,MagnetReturn,QuickCatch,Whirlwind,LongArc,SkimmingStone,CrescentGuard};
        public static bool Is(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal)&&Array.IndexOf(Ids,id)>=0;
        public static int Index(string id)=>Array.IndexOf(Ids,id);
        /// <summary>How long the draft holds on a pick of one of these (s): the plant, then its animation (Runtime NativeGlaiveChipArt.Reveal) to the end.</summary>
        public const double RevealSeconds=5.2;

        // ---- the numbers (the rules', the effects' and the cards') ----
        /// <summary>Fourth Bounce: ricochets more, the look for the next target farther (m), the share of the hit each ricochet keeps.</summary>
        public const int BounceMore=2;
        public const float BounceLook=4,BounceKeep=.9f;
        /// <summary>Keen Edge, a copy: more Glaive damage; every cut's bleed (a second, seconds), refreshed by the next; how often the effects
        /// show a drip (s).</summary>
        public const int KeenMax=2;
        public const float KeenDamage=.1f,BleedDps=3,BleedFor=2,BleedShow=.5f;
        /// <summary>Twin Orbit: the second ring circles the other way this much farther out (m).</summary>
        public const float TwinOut=1.2f;
        /// <summary>Shatterpoint: the burst this much wider; its shards: how many, how far (m), how fast (m/s), how close they cut (m), each cut.</summary>
        public const float ShatterWide=1.4f,ShardRange=5,ShardSpeed=25,ShardRadius=.5f,ShardDamage=8;
        public const int Shards=6;
        /// <summary>Magnet Return: how far the returning ring drags each one it cuts toward you (m), never closer than MagnetClose.</summary>
        public const float MagnetDrag=3,MagnetClose=1.2f;
        /// <summary>Quick Catch: off Q's cooldown at the catch (s), the move speed and how long.</summary>
        public const float QuickCut=2.5f,QuickSpeed=1.25f,QuickFor=1.5f;
        /// <summary>Whirlwind: the whirl's reach (m), how far it drags them in before it cuts (m), never closer than WhirlClose, its damage.</summary>
        public const float WhirlReach=3.5f,WhirlDrag=1.5f,WhirlClose=.9f,WhirlDamage=1.2f;
        /// <summary>Long Arc: the flick's loop farther and wider, its cuts harder.</summary>
        public const float ArcReach=1.6f,ArcWide=1.4f,ArcDamage=1.25f;
        /// <summary>Skimming Stone: the skim farther; the slick it leaves (s after the skim), its half width (m) and the speed it leaves
        /// enemies on it (40% slower), refreshed while they stand there.</summary>
        public const float SkimFarther=1.5f,SlickTime=2,SlickWidth=1.1f,SlickSlow=.6f,SlickHold=.3f;
        /// <summary>Crescent Guard: the share of damage taken while the ring is out of the hand; how far round the halo's ring a projectile is
        /// cut down (m, past the ring's own reach and the shot's radius).</summary>
        public const float GuardTaken=.75f,GuardReach=.25f;

        /// <summary>The rule each chip needs (all of them: none is a plain stat).</summary>
        public static readonly string[] Mechanics=Ids;
        private static ContentChip Chip(int i,string name,int max,string description)=>new ContentChip{id=Ids[i],name=name,description=description,rarity=Tier,max=max,icon="orbit",tags=new[]{Tier},excludes=Array.Empty<string>(),mechanic=Mechanics[i],effects=Array.Empty<ChipEffect>()};
        public static ContentChip[] Chips()=>new[]{
            Chip(0,"Fourth Bounce",1,"Ricochet Throw ricochets 2 more times (5 in all) and looks 4 m farther for the next target. Each ricochet keeps 90% of the hit, not 85%."),
            Chip(1,"Keen Edge",KeenMax,"+10% Glaive damage a copy (cuts, skim, throw, return, burst, halo). Every cut makes them bleed 3 a second for 2 s, refreshed by the next."),
            Chip(2,"Twin Orbit",1,"Halo sends a second ring circling the other way 1.2 m farther out, cutting everyone it passes with its own hits."),
            Chip(3,"Shatterpoint",1,"The Burst is 40% wider and throws out 6 shards that fly 5 m, cutting whoever they pass for 8 each."),
            Chip(4,"Magnet Return",1,"The returning ring drags each enemy it cuts 3 m toward you."),
            Chip(5,"Quick Catch",1,"Catching the thrown or circling ring takes 2.5 s off Q's cooldown and gives +25% move speed for 1.5 s."),
            Chip(6,"Whirlwind",1,"The combo's whirl reaches 3.5 m and drags enemies 1.5 m in before it cuts, for +20% whirl damage."),
            Chip(7,"Long Arc",1,"The flick's boomerang loop goes 60% farther and 40% wider, and its cuts deal +25% damage."),
            Chip(8,"Skimming Stone",1,"The skim lunges 50% farther and leaves a slick of light for 2 s that slows enemies on it by 40%."),
            Chip(9,"Crescent Guard",1,"While the ring is out of your hand (thrown, flicked or circling) you take 25% less damage, and the halo cuts down enemy shots it passes.")};
        /// <summary>Adds the rows to the content (nothing if they are there). For the live content load; the Dev Sandbox's preview calls it on its own copy.</summary>
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(c=>Is(c.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            // The Glaive throws no projectiles of the ordinary kind (its ring is its own): their chips are not for it.
            var blocked=content.chips.Where(c=>c.tags?.Contains("projectile")==true).Select(c=>c.id);
            var row=content.incompatibilities.FirstOrDefault(x=>x.spec==NativeGlaive.Spec);
            if(row==null){row=new ExclusionData{spec=NativeGlaive.Spec,ids=Array.Empty<string>()};content.incompatibilities=content.incompatibilities.Concat(new[]{row}).ToArray();}
            row.ids=row.ids.Concat(blocked).Distinct().ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nglaive-chips-rules-v1");
        }
        /// <summary>The line under the card when one goes in.</summary>
        public static string Confirm(string id)
        {
            switch(id){case FourthBounce:return "AND ANOTHER";case KeenEdge:return "RAZOR SHARP";case TwinOrbit:return "DOUBLE TROUBLE";case Shatterpoint:return "BREAK POINT";
                case MagnetReturn:return "COME HERE";case QuickCatch:return "NICE CATCH";case Whirlwind:return "SPIN TO WIN";case LongArc:return "THE LONG WAY ROUND";
                case SkimmingStone:return "SKIP, SKIP, SKIP";case CrescentGuard:return "EDGE OF DEFENSE";default:return "EDGE SHARPENED";}
        }

        // ---- the shared paths (the rules and the effects read the same ones) ----
        /// <summary>Twin Orbit's second ring at `time` round the wielder's middle `centre`: begun opposite the halo's ring, circling the other way
        /// TwinOut farther out and a little higher.</summary>
        public static Vector3 TwinAt(Vector3 centre,Vector3 dir,float at,float time)
        {
            dir.Y=0;float l=dir.Length();var d=l>1e-5f?dir/l:Vector3.UnitZ;
            float a0=(float)Math.Atan2(d.X,d.Z)+(float)Math.PI,a=a0-(float)(2*Math.PI*NativeGlaive.HaloTurns*Math.Max(0,time-at));
            float grow=Math.Min(1,Math.Max(0,(time-at)/.24f));float r=(NativeGlaive.HaloRadius+TwinOut)*(.3f+.7f*grow);
            return centre+new Vector3((float)Math.Sin(a)*r,.12f-.06f*(float)Math.Sin(a*2),(float)Math.Cos(a)*r);
        }
        /// <summary>Shatterpoint's shard `i` from the burst whose hit is `hit` (its id seeds the turn): flat, unit.</summary>
        public static Vector3 ShardDir(int hit,int i)
        {
            double a=(Math.Abs(hit)%997)*2.39996323+i*2*Math.PI/Shards;return new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
        }
        /// <summary>How far a shard has flown `age` s after the burst (m).</summary>
        public static float ShardTravel(float age)=>Math.Max(0,Math.Min(ShardRange,ShardSpeed*age));
        /// <summary>Skimming Stone's slick: on from the skim's release (`at`) through the skim and SlickTime after it.</summary>
        public static bool SlickOn(float at,float time)=>at>-50&&time>=at&&time<at+NativeGlaive.SkimTime+SlickTime;

        // ---- the pick's cues (Runtime NativeGlaiveChipArt.Reveal, NativeGlaivePresentation's GlaivePick) ----
        /// <summary>Each chip's pick, on its animation's clock (s after it starts), in time order: the ring's own synthesized cues
        /// (Core NativeToonSynth "ring-*") and a few of the shared ones.</summary>
        public static readonly NativeTopDogSounds.IntroCue[][] Picks=BuildPicks();
        private static NativeTopDogSounds.IntroCue[][] BuildPicks()
        {
            NativeTopDogSounds.IntroCue C(float at,string id,float gain,float rate=1)=>new NativeTopDogSounds.IntroCue(at,id,gain,rate);
            var l=new[]{
                // Fourth Bounce: thrown, four pings round the corners (each higher), the catch.
                new[]{C(0,"ring-throw",.8f),C(.18f,"ring-ping",.8f),C(.46f,"ring-ping",.8f,1.08f),C(.74f,"ring-ping",.85f,1.17f),C(1.02f,"ring-ping",.9f,1.27f),C(1.36f,"ring-catch",.9f),C(1.38f,"ring-chime",.6f)},
                // Keen Edge: the glint's swish, the cut, the seal's shimmer.
                new[]{C(.02f,"ring-cut",.7f,1.2f),C(.36f,"ring-slice",1),C(.38f,"ring-ping",.5f,1.5f),C(1.05f,"ring-chime",.7f),C(1.5f,"ring-chime",.5f,1.25f)},
                // Twin Orbit: two throws, the hum of both, the meeting.
                new[]{C(.05f,"ring-throw",.7f),C(.14f,"ring-throw",.7f,1.2f),C(.3f,"ring-hum",.6f),C(1.05f,"ring-hum",.6f,1.15f),C(1.82f,"ring-ping",.8f),C(1.84f,"ring-ping",.7f,1.33f),C(1.88f,"ring-chime",.7f)},
                // Shatterpoint: the charge, the cracks, the burst, the shards' pings.
                new[]{C(0,"ring-hum",.5f,.8f),C(.22f,"ring-slice",.7f,.75f),C(.36f,"ring-slice",.7f,.9f),C(.5f,"ring-burst",1),C(.62f,"ring-ping",.5f,1.4f),C(.78f,"ring-ping",.4f,1.6f)},
                // Magnet Return: the field rising, the pull, the ring taking them in.
                new[]{C(0,"ring-hum",.6f,.7f),C(.3f,"slide-up",.45f,.8f),C(.85f,"ring-hum",.6f,.9f),C(1.55f,"ring-catch",.9f),C(1.57f,"ring-chime",.6f)},
                // Quick Catch: flicked off, out, back, caught and away.
                new[]{C(.15f,"ring-flick",.8f),C(.2f,"ring-throw",.6f),C(.6f,"ring-hum",.5f,1.1f),C(1,"ring-catch",1),C(1.02f,"ring-ping",.6f,1.3f),C(1.06f,"zip",.6f)},
                // Whirlwind: the wind rising round, the whirl, the cut.
                new[]{C(0,"ring-hum",.5f,.6f),C(.2f,"whirl",.6f),C(.7f,"ring-hum",.6f,.8f),C(.9f,"whirl",.6f,1.2f),C(1.36f,"ring-cut",.9f,.8f),C(1.4f,"ring-burst",.6f)},
                // Long Arc: the throw, the long hum out, the turn, the catch.
                new[]{C(.05f,"ring-throw",.8f),C(.4f,"ring-hum",.6f,.9f),C(1,"ring-flick",.7f),C(1.72f,"ring-catch",.9f),C(1.75f,"ring-chime",.6f)},
                // Skimming Stone: the throw, three skips, the slick's shimmer.
                new[]{C(0,"ring-throw",.7f,1.2f),C(.32f,"ring-skip",.9f),C(.72f,"ring-skip",.8f,1.12f),C(1.05f,"ring-skip",.7f,1.25f),C(1.3f,"ring-chime",.5f,1.2f)},
                // Crescent Guard: the sweep, the hum, the hexes, the shot cut down.
                new[]{C(0,"ring-cut",.7f,.7f),C(.2f,"ring-hum",.5f),C(.9f,"ring-chime",.8f),C(1.4f,"ring-ping",.9f),C(1.42f,"ring-slice",.6f,1.3f),C(1.5f,"ring-chime",.5f,.8f)},
            };
            foreach(var a in l)Array.Sort(a,(x,y)=>x.At.CompareTo(y.At));return l;
        }

        // ---- the emblems: flat shapes in a 100 box, one per chip (polygons, each turned the same way, holes the other) ----
        public const float GlyphBox=100;
        private static string F(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        private static string Poly(List<Vector2> p,bool hole=false)
        {
            float area=0;for(int i=0,j=p.Count-1;i<p.Count;j=i,i++)area+=(p[j].X*p[i].Y-p[i].X*p[j].Y);
            if(area<0!=hole)p.Reverse();var s=new System.Text.StringBuilder();
            for(int i=0;i<p.Count;i++)s.Append(i==0?"M":"L").Append(F(p[i].X)).Append(' ').Append(F(p[i].Y));return s.Append('Z').ToString();
        }
        private static List<Vector2> Circle(float x,float y,float r,int n=28){var l=new List<Vector2>(n);for(int i=0;i<n;i++){double a=i*2*Math.PI/n;l.Add(new Vector2(x+r*(float)Math.Cos(a),y+r*(float)Math.Sin(a)));}return l;}
        private static string Disc(float x,float y,float r)=>Poly(Circle(x,y,r));
        private static string Annulus(float x,float y,float r0,float r1)=>Poly(Circle(x,y,r0))+Poly(Circle(x,y,r1),true);
        private static string Seg(float x0,float y0,float x1,float y1,float w)
        {
            var d=new Vector2(x1-x0,y1-y0);float l=d.Length();if(l<1e-4f)return "";var n=new Vector2(-d.Y,d.X)/l*(w/2);
            return Poly(new List<Vector2>{new Vector2(x0,y0)+n,new Vector2(x1,y1)+n,new Vector2(x1,y1)-n,new Vector2(x0,y0)-n});
        }
        private static string Tri(float x0,float y0,float x1,float y1,float x2,float y2)=>Poly(new List<Vector2>{new Vector2(x0,y0),new Vector2(x1,y1),new Vector2(x2,y2)});
        // A band along a circle's arc (degrees, y down), `w` wide; `taper` thins both ends to points.
        private static string Arc(float x,float y,float r,float w,float from,float to,bool taper=false,int n=20)
        {
            var o=new List<Vector2>();var i=new List<Vector2>();
            for(int k=0;k<=n;k++){float u=k/(float)n;double a=(from+(to-from)*u)*Math.PI/180;float half=w/2*(taper?(float)Math.Sin(Math.PI*u):1);var d=new Vector2((float)Math.Cos(a),(float)Math.Sin(a));o.Add(new Vector2(x,y)+d*(r+half));i.Add(new Vector2(x,y)+d*(r-half));}
            i.Reverse();o.AddRange(i);return Poly(o);
        }
        // An arrowhead at (x, y) pointing along `deg`.
        private static string Head(float x,float y,float deg,float s){double a=deg*Math.PI/180;var d=new Vector2((float)Math.Cos(a),(float)Math.Sin(a));var n=new Vector2(-d.Y,d.X);var t=new Vector2(x,y);var tip=t+d*s;var b=t-d*(s*.35f);return Poly(new List<Vector2>{tip,b+n*s*.75f,b-n*s*.75f});}
        // The ring blade small: its ring and three crescent fangs swept off the rim with the spin.
        private static string Blade(float x,float y,float r,float turn)
        {
            string d=Annulus(x,y,r,r*.52f);
            for(int k=0;k<3;k++){double a=(turn+k*120)*Math.PI/180;Vector2 P(double ang,float rad)=>new Vector2(x+rad*(float)Math.Cos(ang),y+rad*(float)Math.Sin(ang));
                d+=Poly(new List<Vector2>{P(a-.42,r*.92f),P(a+.1,r*1.25f),P(a+.62,r*1.7f),P(a+.35,r*1.12f),P(a+.24,r*.9f)});}
            return d;
        }
        private static string Hex(float x,float y,float r,float w)
        {
            var o=new List<Vector2>();var i=new List<Vector2>();for(int k=0;k<6;k++){double a=(k*60+30)*Math.PI/180;var d=new Vector2((float)Math.Cos(a),(float)Math.Sin(a));o.Add(new Vector2(x,y)+d*r);i.Add(new Vector2(x,y)+d*(r-w));}
            return Poly(o)+Poly(i,true);
        }
        private static string Spark(float x,float y,float r)=>Poly(new List<Vector2>{new Vector2(x,y-r),new Vector2(x+r*.2f,y-r*.2f),new Vector2(x+r,y),new Vector2(x+r*.2f,y+r*.2f),new Vector2(x,y+r),new Vector2(x-r*.2f,y+r*.2f),new Vector2(x-r,y),new Vector2(x-r*.2f,y-r*.2f)});
        private static string Path(string d)=>"<path d=\""+d+"\" fill=\"#fff\"/>";
        private static readonly string[] glyphs=new string[10];
        /// <summary>Chip `id`'s emblem as markup in a <see cref="GlyphBox"/> box, or null when it is not one of the set.</summary>
        public static string Glyph(string id){int i=Index(id);if(i<0)return null;return glyphs[i]??(glyphs[i]=Path(GlyphPath(i)));}
        private static string GlyphPath(int i)
        {
            switch(i)
            {
                // A zigzag of four bounces into the ring, a spark at each turn.
                case 0:return Seg(6,18,26,52,6)+Seg(26,52,42,20,6)+Seg(42,20,56,50,6)+Seg(56,50,64,36,6)+Spark(26,56,7)+Spark(42,16,7)+Spark(56,54,7)+Blade(76,62,13,-30);
                // A long thin crescent edge, a glint on it, a cut across.
                case 1:return Arc(50,58,36,10,-160,-20,true,24)+Seg(14,86,88,14,3.5f)+Spark(70,26,12)+Disc(70,26,3);
                // Two rings on one orbit going opposite ways.
                case 2:return Arc(50,50,38,4,0,360,false,36)+Blade(50,12,10,0)+Blade(50,88,10,60)+Head(84,40,100,9)+Head(16,60,-80,9);
                // The ring broken, its six shards flung out.
                case 3:return Arc(50,50,12,8,20,140)+Arc(50,50,12,8,170,290)+Arc(50,50,12,8,310,350)+Tri(50,8,56,30,44,30)+Tri(88,28,70,42,66,32)+Tri(88,74,66,68,70,58)+Tri(50,92,44,70,56,70)+Tri(12,74,34,68,30,58)+Tri(12,28,30,42,34,32);
                // A horseshoe magnet pulling the ring in.
                case 4:return Arc(30,50,24,12,90,270)+Seg(30,26,46,26,12)+Seg(30,74,46,74,12)+Seg(46,22,54,22,4)+Seg(46,78,54,78,4)+Blade(76,50,11,10)+Seg(52,38,62,44,3)+Seg(52,62,62,56,3);
                // The ring going out and boomeranging back, speed lines behind.
                case 5:return Blade(28,56,13,-20)+Arc(56,44,26,7,200,470,false,30)+Head(37,36,150,11)+Seg(4,76,22,76,4)+Seg(8,86,26,86,4)+Seg(6,66,16,66,4);
                // A vortex: three arcs winding in.
                case 6:return Arc(50,50,40,8,-30,200,true,28)+Arc(50,50,27,8,100,330,true,24)+Arc(50,50,14,8,220,450,true,20)+Disc(50,50,5);
                // A long boomerang loop across the box, the ring at its head.
                case 7:return Arc(46,52,40,6,170,420,true,32)+Arc(46,52,22,5,60,250,true,24)+Blade(84,40,10,40)+Head(70,82,170,9);
                // A stone skipping three times over water, the ripples.
                case 8:return Arc(22,74,14,4,180,360,false,14)+Arc(46,74,10,4,180,360,false,12)+Arc(64,74,7,4,180,360,false,10)+Seg(4,82,96,82,5)+Seg(12,92,88,92,3)+Poly(Circle(84,72,9,20).Select(v=>new Vector2(v.X,72+(v.Y-72)*.42f)).ToList());
                // A crescent cradling a dome of hexes.
                default:return Arc(50,52,40,12,30,330,true,30)+Hex(50,48,12,4)+Hex(30,48,9,3)+Hex(70,48,9,3)+Hex(40,30,8,3)+Hex(60,30,8,3);
            }
        }
    }
}
