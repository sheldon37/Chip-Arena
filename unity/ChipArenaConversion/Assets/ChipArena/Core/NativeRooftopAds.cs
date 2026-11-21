using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>One ad of the rooftop's holo screens: what it sells (a class, a stage, a mode, the game), its words and its look.</summary>
    public sealed class RooftopSlide
    {
        public const int Class=0,Stage=1,Mode=2,House=3,Logo=4;
        /// <summary>Its id (a weapon's, a stage's map id, "expedition", "roster", "logo"), kind, words (capitals, digits and
        /// hyphens: the art's glyphs), accent (#rrggbb) and, for a class, its family ("ranged", "melee", "magic").</summary>
        public string Id,Title,Sub,Kicker,Ticker,Accent,Family;public int Kind;
    }
    /// <summary>
    /// The rooftop holo screens' rotating ads (owner 2026-11-11: "Changing animations happening on the TV screens as well, since
    /// things on there are out dated"; docs/MENU-ROOFTOP-ART.md): the public classes (the content's weapons that are not DEV
    /// only), the nine remodelled stages, the Expedition and the game's logo. Nothing DEV only or unreleased is named here (no
    /// DEV class, no boss). Presentation only, engine free and deterministic: each slide is SVG markup for NativeIconRaster on a
    /// 100-unit square, in two layers the screen shader moves apart (Resources/NativeRooftopScreen.shader):
    ///  - the picture (rgb): a backdrop, the kicker, the title band (the title and its line) and the ticker strip, laid out in
    ///    the bands <see cref="Bands"/> names so the shader can push the picture in, slide the title and scroll the ticker;
    ///  - the mask (alpha): what strikes in over the picture in the slide's accent: a pilot's silhouette in its family's pose
    ///    beside the class's own icon, a stage's emblem, the Expedition's drop pod.
    /// Rasterised on worker threads by the Runtime (Compose), as the old ads were.
    /// </summary>
    public static class NativeRooftopAds
    {
        public const int Pixels=320;
        /// <summary>The seconds a slide shows on the class, stage and brand screens; each rotation's length divides the scene
        /// clock's wrap (<see cref="Wrap"/> s), so the clock's wrap never cuts a slide short.</summary>
        public const float ClassSeconds=6,StageSeconds=8,BrandSeconds=12,Wrap=4800;
        /// <summary>The bands of a slide down its visible height (0 top .. 1 bottom): the picture's foot, the title band's foot,
        /// the ticker's top.</summary>
        public const float PictureFoot=.62f,TitleFoot=.865f,TickerTop=.885f;
        /// <summary>Where the mask's middle sits across the picture (0 left .. 1 right), for the shader's strike.</summary>
        public const float MaskX=.5f;
        private const string Ink="#eef6ff",Dark="#05070f";
        private static string F(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        // The accent mixed into the dark field (the markup rasteriser takes opacity only in its art mode).
        private static string Tint(string accent,float share)
        {
            int v=int.Parse(accent.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture),d=int.Parse(Dark.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
            int M(int sh)=>(int)Math.Round(((d>>sh)&255)+(((v>>sh)&255)-((d>>sh)&255))*share);return "#"+((M(16)<<16)|(M(8)<<8)|M(0)).ToString("x6",CultureInfo.InvariantCulture);
        }
        private static readonly Dictionary<string,(string accent,string sub)> classes=new Dictionary<string,(string,string)>
        {
            ["pistol"]=("#5ff4ff","QUICK DRAW"),["rifle"]=("#7dffb0","ONE SHOT"),["machinegun"]=("#ffc24d","HOLD THE LINE"),["fire"]=("#ff8a3d","BURN IT DOWN"),
            ["water"]=("#5aa8ff","RIDE THE TIDE"),["lightning"]=("#ffe45a","STRIKE TWICE"),["darkness"]=("#b48cff","FADE OUT"),["sword"]=("#ff4fd8","PARRY AND PUNISH"),
            ["daggers"]=("#ff5a6a","IN AND OUT"),["shield"]=("#5ff4ff","STAND FIRM"),["greatsword"]=("#ffc24d","CLEAVE THROUGH"),["bow"]=("#7dffb0","LOOSE"),
            ["glaive"]=("#b48cff","FULL CIRCLE"),["katana"]=("#ff5a6a","ONE CUT"),
        };
        /// <summary>The nine remodelled stages (2026-11-11) and the Expedition, in the stage screen's order.</summary>
        public static readonly RooftopSlide[] Stages=
        {
            Stage("breakwater","BREAKWATER","LIGHTHOUSE DUEL","#5aa8ff",1),Stage("harbor","PIER 9","NIGHT ON THE PIER","#ff4fd8",2),Stage("skyframe","SKYFRAME","ABOVE THE CLOUDS","#ffc24d",3),
            Stage("rig","DEEPWELL","OCEAN FLOOR","#5ff4ff",4),Stage("polar","POLARIS STATION","UNDER THE AURORA","#7dffb0",5),
            new RooftopSlide{Id="expedition",Kind=RooftopSlide.Mode,Title="EXPEDITION",Sub="DEPLOY TOGETHER",Kicker="CO-OP MODE",Ticker="START EXPEDITION - SQUAD UP - DROP IN",Accent="#ffc24d"},
            Stage("brickfall","BRICKFALL","MIND THE WALLS","#ff8a3d",6),Stage("siren","WARD ZERO","LIGHTS OUT","#ff5a6a",7),Stage("factory","THE FACTORY","SHIFT CHANGE","#ffc24d",8),
            Stage("cybercity","CYBER CITY","NEON RAIN","#b48cff",9),
        };
        private static RooftopSlide Stage(string id,string title,string sub,string accent,int n)=>new RooftopSlide{Id=id,Kind=RooftopSlide.Stage,Title=title,Sub=sub,Kicker="STAGE 0"+n+" OF 09",Ticker="REBUILT - "+title+" - PLAY IT NOW",Accent=accent};
        /// <summary>The brand screen's rotation: the logo between two teasers.</summary>
        public static readonly RooftopSlide[] Brand=
        {
            new RooftopSlide{Id="logo",Kind=RooftopSlide.Logo,Title="CHIP ARENA",Sub="",Kicker="",Ticker="",Accent="#ff8a3d"},
            new RooftopSlide{Id="cybercity",Kind=RooftopSlide.House,Title="NINE STAGES",Sub="ALL REBUILT",Kicker="NEW",Ticker="NINE STAGES REBUILT - PICK ONE",Accent="#5ff4ff"},
            new RooftopSlide{Id="logo",Kind=RooftopSlide.Logo,Title="CHIP ARENA",Sub="",Kicker="",Ticker="",Accent="#ff8a3d"},
            new RooftopSlide{Id="expedition",Kind=RooftopSlide.Mode,Title="EXPEDITION",Sub="DEPLOY TOGETHER",Kicker="CO-OP MODE",Ticker="START EXPEDITION - SQUAD UP - DROP IN",Accent="#ffc24d"},
        };
        /// <summary>The class screen's rotation from the content's weapons (id, name, class; DEV-only ones left out by the
        /// caller's flag): one slide a public class, then house slides (the roster) until the count divides the wrap.</summary>
        public static RooftopSlide[] Classes(IList<ContentWeapon> weapons)
        {
            var list=new List<RooftopSlide>();
            if(weapons!=null)foreach(var w in weapons)
            {
                if(w==null||w.devOnly||string.IsNullOrEmpty(w.id)||string.IsNullOrEmpty(w.name))continue;string title=Words(w.name);if(title.Length==0)continue;
                string family=w.@class=="melee"||w.@class=="magic"?w.@class:"ranged";var look=classes.TryGetValue(w.id,out var c)?c:(family=="melee"?"#ff4fd8":family=="magic"?"#b48cff":"#5ff4ff","PICK YOURS");
                list.Add(new RooftopSlide{Id=w.id,Kind=RooftopSlide.Class,Title=title,Sub=look.Item2,Kicker=family.ToUpperInvariant()+" CLASS",Ticker="NOW PLAYING - "+title+" - "+look.Item2,Accent=look.Item1,Family=family});
            }
            int count=list.Count;int slots=Math.Max(1,(int)Math.Round(Wrap/ClassSeconds));
            do list.Add(new RooftopSlide{Id="roster",Kind=RooftopSlide.House,Title=count+" CLASSES",Sub="PICK YOURS",Kicker="THE ROSTER",Ticker="RANGED - MELEE - MAGIC - "+count+" CLASSES",Accent=list.Count%2==0?"#5ff4ff":"#ff4fd8"});
            while(slots%list.Count!=0&&list.Count<slots);
            return list.ToArray();
        }
        /// <summary>A name in the art's glyphs: capitals, digits, hyphens and spaces; anything else dropped.</summary>
        public static string Words(string text){var sb=new StringBuilder();foreach(char ch in (text??"").ToUpperInvariant())if(ch==' '||NativeRooftopArt.Has(ch))sb.Append(ch);return sb.ToString().Trim();}
        /// <summary>The slide's bands as the shader takes them (x the picture's foot, y the title's foot, z the ticker's top; a
        /// logo is all picture).</summary>
        public static (float picture,float title,float ticker) Bands(RooftopSlide s)=>s.Kind==RooftopSlide.Logo?(1f,1f,1f):(PictureFoot,TitleFoot,TickerTop);
        /// <summary>The accent as linear-ish rgb 0..1 (sRGB values; the shader works in the texture's space).</summary>
        public static (float r,float g,float b) Accent(RooftopSlide s)
        {
            string h=s?.Accent;if(h==null||h.Length!=7)return (.37f,.96f,1f);
            int v=int.Parse(h.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);return (((v>>16)&255)/255f,((v>>8)&255)/255f,(v&255)/255f);
        }
        // A text run centred or left at a cap height, shrunk to fit `max` units; returns its width.
        private static float Fit(StringBuilder sb,string text,float x,float y,float height,float max,string color,bool centre,float weight=1)
        {
            float spacing=9,w=NativeRooftopArt.Width(text,spacing)*(height/NativeRooftopArt.GlyphHeight);if(w>max){height*=max/w;w=max;}
            NativeRooftopArt.Text(sb,text,centre?x-w/2:x,y,height,spacing,color,Math.Max(.7f,height*.115f*weight));return w;
        }
        /// <summary>The picture layer's markup on the visible rows y0..y1 of the 100-unit square (a 4:3 screen shows 12.5..87.5).</summary>
        public static string Picture(RooftopSlide s,float y0=0,float y1=100)
        {
            var sb=new StringBuilder();float H=y1-y0;string a=s.Accent??"#5ff4ff";
            float pf=y0+H*PictureFoot,tf=y0+H*TitleFoot,tt=y0+H*TickerTop;
            if(s.Kind==RooftopSlide.Logo)
            {
                // the Runtime lays the owner's logo over this; without it, the wordmark in the art's glyphs
                sb.Append("<rect x=\"0\" y=\""+F(y0)+"\" width=\"100\" height=\""+F(H)+"\" fill=\"#0a0614\"/>");
                Fit(sb,"CHIP",50,y0+H*.2f,H*.24f,84,"#fff0d0",true,1.2f);Fit(sb,"ARENA",50,y0+H*.54f,H*.2f,84,"#ff8a3d",true,1.2f);
                return sb.ToString();
            }
            // the backdrop: a dark field, the accent in slanted bars and a disc behind the mask, a dot grid
            sb.Append("<rect x=\"0\" y=\""+F(y0)+"\" width=\"100\" height=\""+F(H)+"\" fill=\""+Dark+"\"/>");
            sb.Append("<path d=\"M0 "+F(pf)+"L0 "+F(y0+H*.2f)+"L"+F(H*.42f)+" "+F(y0)+"L"+F(H*.7f)+" "+F(y0)+"L"+F(H*.08f)+" "+F(pf)+"Z\" fill=\""+Tint(a,.10f)+"\"/>");
            sb.Append("<path d=\"M"+F(H*.2f)+" "+F(pf)+"L"+F(H*.82f)+" "+F(y0)+"L"+F(H*.9f)+" "+F(y0)+"L"+F(H*.28f)+" "+F(pf)+"Z\" fill=\""+Tint(a,.18f)+"\"/>");
            sb.Append("<circle cx=\"50\" cy=\""+F(y0+H*.33f)+"\" r=\""+F(H*.25f)+"\" fill=\""+Tint(a,.24f)+"\"/><circle cx=\"50\" cy=\""+F(y0+H*.33f)+"\" r=\""+F(H*.25f)+"\" fill=\"none\" stroke=\""+Tint(a,.6f)+"\" stroke-width=\".6\"/>");
            for(int j=0;j<4;j++)for(int i=0;i<4;i++)sb.Append("<circle cx=\""+F(84+i*3.2f)+"\" cy=\""+F(y0+H*.07f+j*3.2f)+"\" r=\".5\" fill=\""+Tint(a,.6f)+"\"/>");
            // the kicker (top left) on its tab
            if(!string.IsNullOrEmpty(s.Kicker)){sb.Append("<rect x=\"4\" y=\""+F(y0+H*.05f)+"\" width=\"1.6\" height=\""+F(H*.06f)+"\" fill=\""+a+"\"/>");Fit(sb,s.Kicker,7.5f,y0+H*.055f,H*.05f,60,a,false);}
            // the title band: a rule, the title, its line
            sb.Append("<rect x=\"0\" y=\""+F(pf)+"\" width=\"100\" height=\""+F(tf-pf)+"\" fill=\"#0b1020\"/><rect x=\"0\" y=\""+F(pf)+"\" width=\"100\" height=\".9\" fill=\""+a+"\"/>");
            Fit(sb,s.Title,50,pf+H*.035f,H*.115f,90,Ink,true,1.15f);
            if(!string.IsNullOrEmpty(s.Sub))Fit(sb,s.Sub,50,pf+H*.178f,H*.045f,80,a,true);
            // the ticker: the phrase twice, 50 units apart, so the shader's scroll wraps without a seam
            sb.Append("<rect x=\"0\" y=\""+F(tf)+"\" width=\"100\" height=\""+F(y1-tf)+"\" fill=\"#02030a\"/><rect x=\"0\" y=\""+F(tt)+"\" width=\"100\" height=\""+F(y1-tt)+"\" fill=\""+Tint(a,.22f)+"\"/>");
            if(!string.IsNullOrEmpty(s.Ticker))for(int k=0;k<2;k++)Fit(sb,s.Ticker,2+k*50,tt+(y1-tt)*.22f,(y1-tt)*.56f,45,Ink,false);
            return sb.ToString();
        }
        // ---- the mask layer: white shapes; the Runtime (Compose) keeps their coverage as the texture's alpha ----
        private static void Limb(StringBuilder sb,float ox,float oy,float k,float width,params float[] p)
        {
            sb.Append("<path d=\"M");for(int i=0;i+1<p.Length;i+=2){if(i>0)sb.Append('L');sb.Append(F(ox+p[i]*k)).Append(' ').Append(F(oy+p[i+1]*k));}
            sb.Append("\" fill=\"none\" stroke=\"#fff\" stroke-width=\""+F(width*k)+"\"/>");
        }
        private static void Shape(StringBuilder sb,float ox,float oy,float k,params float[] p)
        {
            sb.Append("<path d=\"M");for(int i=0;i+1<p.Length;i+=2){if(i>0)sb.Append('L');sb.Append(F(ox+p[i]*k)).Append(' ').Append(F(oy+p[i+1]*k));}
            sb.Append("Z\" fill=\"#fff\"/>");
        }
        private static void Dot(StringBuilder sb,float ox,float oy,float k,float x,float y,float r)=>sb.Append("<circle cx=\""+F(ox+x*k)+"\" cy=\""+F(oy+y*k)+"\" r=\""+F(r*k)+"\" fill=\"#fff\"/>");
        // A pilot's silhouette striking its family's pose, in a 40 x 56 box at (ox, oy) scaled by k, facing left.
        private static void Pilot(StringBuilder sb,string family,float ox,float oy,float k)
        {
            if(family=="melee")
            {
                Dot(sb,ox,oy,k,19,9,4.6f);Shape(sb,ox,oy,k,13,14,27,14,26,32,17,32);
                Limb(sb,ox,oy,k,4.2f,26,16,33,10,35,3);Limb(sb,ox,oy,k,4.2f,15,17,9,22,4,20);
                Limb(sb,ox,oy,k,4.8f,19,31,10,40,4,54);Limb(sb,ox,oy,k,4.8f,25,31,32,41,38,54);
                Shape(sb,ox,oy,k,36.5f,4.5f,33.5f,1.5f,8,-9,6,-11.5f);                       // the blade, raised back over the shoulder
                Shape(sb,ox,oy,k,26,15,31,34,38,44,27,30);                                   // a coat's tail
            }
            else if(family=="magic")
            {
                Dot(sb,ox,oy,k,21,9,4.6f);Shape(sb,ox,oy,k,14,14,28,14,27,32,17,32);
                Limb(sb,ox,oy,k,4.2f,16,16,10,10,8,2);Limb(sb,ox,oy,k,4.2f,27,16,33,22,36,28);
                Limb(sb,ox,oy,k,4.8f,20,31,17,42,15,54);Limb(sb,ox,oy,k,4.8f,25,31,28,42,29,54);
                Dot(sb,ox,oy,k,7,-5,4.4f);Shape(sb,ox,oy,k,7,-13,8.6f,-8,7,-9.6f,5.4f,-8);Shape(sb,ox,oy,k,-1,-5,4,-6.4f,2.4f,-5,4,-3.6f);Shape(sb,ox,oy,k,15,-5,10,-6.4f,11.6f,-5,10,-3.6f);
                Shape(sb,ox,oy,k,15,15,28,15,36,48,22,40);                                   // a cloak
            }
            else
            {
                Dot(sb,ox,oy,k,22,8,4.6f);Shape(sb,ox,oy,k,15,13,28,13,27,32,19,32);
                Limb(sb,ox,oy,k,4.2f,16,16,9,17,2,16);Limb(sb,ox,oy,k,4.2f,27,16,18,21,8,18);
                Limb(sb,ox,oy,k,4.8f,20,31,14,41,10,54);Limb(sb,ox,oy,k,4.8f,26,31,30,42,34,54);
                Shape(sb,ox,oy,k,4,13.4f,4,17.4f,-9,17,-9,14.6f);Shape(sb,ox,oy,k,1,17,4,17,3,22,.5f,22);   // the gun levelled, its grip
                Shape(sb,ox,oy,k,27,14,31,13,39,30,31,27);                                   // a scarf's tail
            }
        }
        /// <summary>The mask layer's markup (white on nothing) on the visible rows y0..y1. A class leaves the left of the
        /// picture clear for its icon (<see cref="IconRect"/>).</summary>
        public static string Mask(RooftopSlide s,float y0=0,float y1=100)
        {
            var sb=new StringBuilder();float H=y1-y0,cy=y0+H*.33f;
            if(s.Kind==RooftopSlide.Logo)return "";
            if(s.Kind==RooftopSlide.Class){float k=H*.5f/56;Pilot(sb,s.Family,58,cy-k*24,k);return sb.ToString();}
            if(s.Id=="roster"){float k=H*.4f/56;Pilot(sb,"ranged",14,cy-k*22,k);Pilot(sb,"melee",42,cy-k*22,k);Pilot(sb,"magic",70,cy-k*22,k);return sb.ToString();}
            // a stage's emblem (or the Expedition's pod) in a 60 x 44 box about the picture's middle
            float u=H*.5f/44,ox=50-30*u,oy=cy-22*u;
            switch(s.Id)
            {
                case "breakwater":   // a lighthouse on its mole, its beam, the swell
                    Shape(sb,ox,oy,u,26,40,34,40,32,12,28,12);Shape(sb,ox,oy,u,26,12,34,12,34,9,26,9);Shape(sb,ox,oy,u,27.5f,9,32.5f,9,32.5f,4,27.5f,4);Shape(sb,ox,oy,u,27,4,33,4,30,0);
                    Shape(sb,ox,oy,u,32.5f,5.5f,60,-1,60,12,32.5f,7.5f);Shape(sb,ox,oy,u,6,44,54,44,50,40,10,40);
                    Limb(sb,ox,oy,u,1.6f,0,34,6,31,12,34,18,31,23,34);Limb(sb,ox,oy,u,1.6f,37,34,43,31,49,34,55,31,60,34);break;
                case "harbor":       // a big wheel over the pier
                    sb.Append("<circle cx=\""+F(ox+30*u)+"\" cy=\""+F(oy+18*u)+"\" r=\""+F(15*u)+"\" fill=\"none\" stroke=\"#fff\" stroke-width=\""+F(2.2f*u)+"\"/>");
                    for(int i=0;i<8;i++){float an=i*(float)Math.PI/4,cx=30+15*(float)Math.Cos(an),cz=18+15*(float)Math.Sin(an);Limb(sb,ox,oy,u,.9f,30,18,cx,cz);Dot(sb,ox,oy,u,cx,cz,2.4f);}
                    Dot(sb,ox,oy,u,30,18,2.6f);Limb(sb,ox,oy,u,2,30,18,22,40);Limb(sb,ox,oy,u,2,30,18,38,40);Shape(sb,ox,oy,u,2,44,58,44,58,40,2,40);break;
                case "skyframe":     // a tower's garden top over the clouds, the sun
                    Shape(sb,ox,oy,u,22,44,38,44,38,14,22,14);Shape(sb,ox,oy,u,18,14,42,14,42,11,18,11);Dot(sb,ox,oy,u,24,8,4.5f);Dot(sb,ox,oy,u,31,6,5.5f);Dot(sb,ox,oy,u,38,8.5f,4);
                    Dot(sb,ox,oy,u,51,10,6);Dot(sb,ox,oy,u,6,36,5);Dot(sb,ox,oy,u,13,34,6.5f);Dot(sb,ox,oy,u,47,36,6);Dot(sb,ox,oy,u,55,37,4.5f);Shape(sb,ox,oy,u,1,36,18,36,18,41,1,41);Shape(sb,ox,oy,u,42,37,60,37,60,41,42,41);break;
                case "rig":          // a dome on the sea bed, a tube, bubbles
                    sb.Append("<path d=\"M"+F(ox+14*u)+" "+F(oy+38*u)+"A"+F(16*u)+" "+F(16*u)+" 0 0 1 "+F(ox+46*u)+" "+F(oy+38*u)+"Z\" fill=\"#fff\"/>");
                    Shape(sb,ox,oy,u,4,44,56,44,56,38,4,38);Shape(sb,ox,oy,u,46,36,58,36,58,31,46,31);Dot(sb,ox,oy,u,10,24,2);Dot(sb,ox,oy,u,14,15,2.8f);Dot(sb,ox,oy,u,9,6,1.8f);Dot(sb,ox,oy,u,50,18,2.4f);Dot(sb,ox,oy,u,53,8,1.6f);break;
                case "polar":        // an observatory under three bands of aurora and a star
                    sb.Append("<path d=\"M"+F(ox+18*u)+" "+F(oy+36*u)+"A"+F(12*u)+" "+F(12*u)+" 0 0 1 "+F(ox+42*u)+" "+F(oy+36*u)+"Z\" fill=\"#fff\"/>");
                    Shape(sb,ox,oy,u,14,44,46,44,46,36,14,36);Shape(sb,ox,oy,u,2,44,60,44,60,41,2,41);
                    Limb(sb,ox,oy,u,2,0,14,12,6,26,10,40,3,60,8);Limb(sb,ox,oy,u,1.4f,0,21,14,13,28,17,44,10,60,15);Shape(sb,ox,oy,u,52,20,53.4f,24.6f,58,26,53.4f,27.4f,52,32,50.6f,27.4f,46,26,50.6f,24.6f);break;
                case "brickfall":    // a wall coming apart
                    for(int r=0;r<4;r++)for(int c=0;c<4;c++){if(r==0&&c>1||r==1&&c==3)continue;float bx=6+c*11+(r%2)*5.5f,by=40-r*7;Shape(sb,ox,oy,u,bx,by,bx+10,by,bx+10,by-6,bx,by-6);}
                    Shape(sb,ox,oy,u,40,12,49,9,51,14,42,17);Shape(sb,ox,oy,u,47,24,56,26,55,31,46,29);Shape(sb,ox,oy,u,31,4,39,2,40,7,32,9);break;
                case "siren":        // a ward's cross in its hexagon, a corridor's lines
                    Shape(sb,ox,oy,u,30,2,46,11,46,31,30,40,14,31,14,11);
                    sb.Append("<path d=\"M"+F(ox+26.5f*u)+" "+F(oy+10*u)+"H"+F(ox+33.5f*u)+"V"+F(oy+17.5f*u)+"H"+F(ox+41*u)+"V"+F(oy+24.5f*u)+"H"+F(ox+33.5f*u)+"V"+F(oy+32*u)+"H"+F(ox+26.5f*u)+"V"+F(oy+24.5f*u)+"H"+F(ox+19*u)+"V"+F(oy+17.5f*u)+"H"+F(ox+26.5f*u)+"Z\" fill=\"#000\"/>");
                    Limb(sb,ox,oy,u,1.4f,0,44,12,36);Limb(sb,ox,oy,u,1.4f,60,44,48,36);break;
                case "factory":      // a gear and a stack
                    for(int i=0;i<8;i++){float an=i*(float)Math.PI/4;float c=(float)Math.Cos(an),sn=(float)Math.Sin(an);Shape(sb,ox,oy,u,22+c*10-sn*3,24+sn*10+c*3,22+c*17-sn*2.4f,24+sn*17+c*2.4f,22+c*17+sn*2.4f,24+sn*17-c*2.4f,22+c*10+sn*3,24+sn*10-c*3);}
                    Dot(sb,ox,oy,u,22,24,12.5f);sb.Append("<circle cx=\""+F(ox+22*u)+"\" cy=\""+F(oy+24*u)+"\" r=\""+F(4.5f*u)+"\" fill=\"#000\"/>");
                    Shape(sb,ox,oy,u,44,44,54,44,52,8,46,8);Dot(sb,ox,oy,u,50,3,3);Dot(sb,ox,oy,u,55,-1,2.2f);break;
                case "expedition":   // a drop pod coming down, its chevrons
                    Shape(sb,ox,oy,u,24,10,30,2,36,10,38,30,30,38,22,30);Shape(sb,ox,oy,u,22,22,14,34,22,30);Shape(sb,ox,oy,u,38,22,46,34,38,30);
                    sb.Append("<circle cx=\""+F(ox+30*u)+"\" cy=\""+F(oy+16*u)+"\" r=\""+F(3.4f*u)+"\" fill=\"#000\"/>");
                    Limb(sb,ox,oy,u,1.8f,6,4,10,9,14,4);Limb(sb,ox,oy,u,1.8f,6,12,10,17,14,12);Limb(sb,ox,oy,u,1.8f,46,4,50,9,54,4);Limb(sb,ox,oy,u,1.8f,46,12,50,17,54,12);
                    Limb(sb,ox,oy,u,1.6f,26,40,24,46);Limb(sb,ox,oy,u,1.6f,30,41,30,48);Limb(sb,ox,oy,u,1.6f,34,40,36,46);break;
                default:             // Cyber City and the brand's stage teaser: a skyline over a maglev's line
                    Shape(sb,ox,oy,u,2,40,10,40,10,18,2,18);Shape(sb,ox,oy,u,12,40,22,40,22,8,17,3,12,8);Shape(sb,ox,oy,u,24,40,32,40,32,22,24,22);Shape(sb,ox,oy,u,34,40,46,40,46,12,34,12);
                    Shape(sb,ox,oy,u,39,12,41,12,41,2,39,2);Shape(sb,ox,oy,u,48,40,58,40,58,26,48,20);Shape(sb,ox,oy,u,0,44,60,44,60,42,0,42);break;
            }
            return sb.ToString();
        }
        /// <summary>Where a class's own icon goes in the mask (the left of the picture), in units of the 100-unit square.</summary>
        public static (float x,float y,float size) IconRect(RooftopSlide s,float y0=0,float y1=100){float H=y1-y0,size=H*.36f;return (30-size/2,y0+H*.33f-size/2,size);}
        /// <summary>
        /// One slide rasterised (worker threads call this): straight RGBA, bottom row first, `px` square: rgb the picture, alpha
        /// the mask's coverage (with the class's icon, art markup on its own `iconBox`, laid into <see cref="IconRect"/>).
        /// </summary>
        public static float[] Compose(RooftopSlide s,string icon,float iconBox,float y0=0,float y1=100,int px=Pixels)
        {
            var rgba=NativeIconRaster.Raster(NativeIconRaster.Parse(Picture(s,y0,y1),1,NativeIconRaster.Mode.Markup),px,100);
            string mask=Mask(s,y0,y1);float[] m=mask.Length>0?NativeIconRaster.Raster(NativeIconRaster.Parse(mask,1,NativeIconRaster.Mode.Markup),px,100):null;
            for(int i=0;i<px*px;i++)rgba[i*4+3]=m==null?0:Math.Min(1,Math.Max(0,m[i*4]*m[i*4+3]));
            if(s.Kind==RooftopSlide.Class&&!string.IsNullOrEmpty(icon)&&iconBox>0)
            {
                var r=IconRect(s,y0,y1);int size=Math.Max(8,(int)Math.Round(r.size/100*px));var cover=NativeIconRaster.Coverage(NativeIconRaster.Parse(icon,1,NativeIconRaster.Mode.Art),size,iconBox);
                // both rasters are bottom row first; the rect's y is from the top
                int ix=(int)Math.Round(r.x/100*px),iy=px-(int)Math.Round(r.y/100*px)-size;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++){int tx=ix+x,ty=iy+y;if(tx<0||ty<0||tx>=px||ty>=px)continue;int k=(ty*px+tx)*4+3;rgba[k]=Math.Max(rgba[k],cover[y*size+x]);}
            }
            return rgba;
        }
    }
}
