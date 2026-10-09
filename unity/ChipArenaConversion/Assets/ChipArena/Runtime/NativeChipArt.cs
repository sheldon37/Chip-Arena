using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The chip card's design layers from chip-cards.css and the exclusive kits' card art (emperor-card.ts, ansatsuken-card.ts,
    // matrix-effects.ts matrixRain, siren-card.ts), drawn by NativeChipCard: the per-tier package materials (legs, substrate,
    // edge, heat spreader, die, label and name colours, the tier glow), the circuit traces, the brushed spreader and its
    // gleam, the die's grid and glass, and the auras: Emperor's storm orbit, bolt, current arcs and grid; Ansatsuken's seal,
    // headband, ki rings and dojo grid; L44T's binary rain; Siren's void glass, black flames, watching grin, scanlines and
    // tear. Textures are built once on first use (never in field initializers); a frame allocates nothing.
    public static class NativeChipArt
    {
        public struct Style
        {
            public Color PinA,PinB,PinC,SubA,SubB,PlateA,PlateB,Edge,Die,Name,Label,Glow,Spark,SparkGlow;
            public float GlowSpread,Traces,Tracking;public bool LabelGlow,Void;public int Pins;
        }
        // --pin-a/b/c, --sub-a/b, --plate-a/b, --edge, --die, --name, the label colour and tracking, and the box-shadow's tier glow.
        public static Style For(string tier,Color rare)
        {
            var s=new Style{PinA=U.Hex(0xf1f3f7),PinB=U.Hex(0xa1a6b1),PinC=U.Hex(0x4c505a),SubA=U.Hex(0x16171d),SubB=U.Hex(0x0d0e12),PlateA=U.Hex(0x2b2d35),PlateB=U.Hex(0x1a1b21),
                Edge=U.Mix(U.Hex(0x34363f),rare,.52f),Die=rare,Name=U.Hex(0xf3f4f8),Label=U.Mix(Color.white,rare,.82f),Spark=rare,SparkGlow=rare,Traces=.1f,Tracking=.08f,Pins=0};
            switch(tier)
            {
                case "common":s.Die=U.Hex(0xe7ebf2);break;
                case "epic":s.SubA=U.Hex(0x19161f);s.Traces=.17f;s.Glow=U.Hex(0xce8aff,.15f);s.GlowSpread=18;break;
                case "legendary":s.PinA=U.Hex(0xfff4c9);s.PinB=U.Hex(0xe3ad4a);s.PinC=U.Hex(0x7b5413);s.PlateA=U.Hex(0x4d3b17);s.PlateB=U.Hex(0x20180a);s.SubA=U.Hex(0x1a160f);s.SubB=U.Hex(0x0e0c08);s.Name=U.Hex(0xffd28c);s.Traces=.2f;s.Glow=U.Hex(0xffae55,.21f);s.GlowSpread=24;s.Pins=1;break;
                case "mythic":s.PinA=U.Hex(0xfff4c9);s.PinB=U.Hex(0xe3ad4a);s.PinC=U.Hex(0x7b5413);s.PlateA=U.Hex(0x2e2532);s.PlateB=U.Hex(0x15111a);s.SubA=U.Hex(0x1b1419);s.SubB=U.Hex(0x0e0a0d);s.Traces=.22f;s.Glow=U.Hex(0xff6475,.28f);s.GlowSpread=30;s.Pins=1;break;
                case "mishima":s.PinA=U.Hex(0xd9dbe3);s.PinB=U.Hex(0x7d808c);s.PinC=U.Hex(0x2c2d34);s.PlateA=U.Hex(0x3b3d48);s.PlateB=U.Hex(0x15151b);s.SubA=U.Hex(0x1c1c22);s.SubB=U.Hex(0x0b0b0f);s.Die=U.Hex(0xff3c5d);s.Edge=U.Hex(0x8f919e);s.Name=U.Hex(0xeceef4);s.Label=U.Hex(0xd6d7df);s.Tracking=.14f;s.Glow=U.Hex(0xff173b,.23f);s.GlowSpread=26;s.Spark=U.Hex(0xff234a);s.SparkGlow=U.Hex(0xf30035);s.Pins=2;break;
                case "ansatsuken":s.PinA=U.Hex(0xffd6da);s.PinB=U.Hex(0xc7475a);s.PinC=U.Hex(0x4a1019);s.PlateA=U.Hex(0x43101d);s.PlateB=U.Hex(0x1a0910);s.SubA=U.Hex(0x200b13);s.SubB=U.Hex(0x10070b);s.Die=U.Hex(0xff4d63);s.Edge=U.Hex(0xff354e);s.Name=U.Hex(0xfff0f1);s.Label=U.Hex(0xff8a97);s.Tracking=.12f;s.Glow=U.Hex(0xff214b,.27f);s.GlowSpread=28;s.Spark=U.Hex(0xff6071);s.SparkGlow=U.Hex(0xff294f);s.Pins=3;break;
                case "l44t":s.PinA=U.Hex(0xd4ffe2);s.PinB=U.Hex(0x3fcf74);s.PinC=U.Hex(0x0b4020);s.PlateA=U.Hex(0x103020);s.PlateB=U.Hex(0x06120c);s.SubA=U.Hex(0x06170e);s.SubB=U.Hex(0x030b07);s.Die=U.Hex(0x5eff91);s.Edge=U.Hex(0x3fd979);s.Name=U.Hex(0xdcffe8);s.Label=U.Hex(0xa0ffc0);s.Tracking=.22f;s.LabelGlow=true;s.Glow=U.Hex(0x36ff78,.26f);s.GlowSpread=30;s.Pins=4;break;
                case "siren":s.PinA=U.Hex(0xd8d1ff);s.PinB=U.Hex(0x3a3150);s.PinC=U.Hex(0x0b0912);s.PlateA=U.Hex(0x1c1527);s.PlateB=U.Hex(0x07060b);s.SubA=U.Hex(0x0b0910);s.SubB=U.Hex(0x050409);s.Die=U.Hex(0xf3efff);s.Edge=U.Hex(0x6d5fb0);s.Name=U.Hex(0xf6f3ff);s.Label=U.Hex(0xc9bfff);s.Tracking=.18f;s.Glow=U.Hex(0x7d5cff,.24f);s.GlowSpread=30;s.Spark=U.Hex(0xd9d0ff);s.SparkGlow=U.Hex(0x9b87ff);s.Void=true;s.Pins=5;break;
            }
            return s;
        }

        // ---- textures (built on first use) ----
        private static Texture2D[] pins;private static Texture2D brush,scan,dieGrid,gloss,diagonal;private const float DiagonalTile=46.669f;
        private static Texture2D Make(int w,int h,bool repeat)=>new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=repeat?TextureWrapMode.Repeat:TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        private static void Fill(Texture2D t,Color[] px){t.SetPixels(px);t.Apply(false,true);}
        private static void Ensure()
        {
            if(brush!=null)return;pins=new Texture2D[6];
            // .circuit-header:before: a 1 px white line every 3 px (4 texels per px).
            brush=Make(12,1,true);var px=new Color[12];for(int x=0;x<12;x++)px[x]=new Color(1,1,1,x<4?1:0);Fill(brush,px);
            // .siren-scan: the same every 3 px down the card (row 0 is the bottom; the line on the top texels).
            scan=Make(1,12,true);px=new Color[12];for(int y=0;y<12;y++)px[y]=new Color(1,1,1,y>=8?1:0);Fill(scan,px);
            // .chip-icon:before: 1 px lines every 7 px, horizontal at 18% and vertical at 14% (the alpha carries the ratio).
            dieGrid=Make(28,28,true);px=new Color[28*28];for(int y=0;y<28;y++)for(int x=0;x<28;x++){bool h=y<4,v=x>=24;px[y*28+x]=new Color(1,1,1,h?1:v?14f/18f:0);}Fill(dieGrid,px);
            // .chip-icon:after: linear-gradient(135deg, #fff 0, transparent 40%), the alpha along the diagonal.
            gloss=Make(32,32,false);px=new Color[32*32];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float u=(x+(31-y))/62f;px[y*32+x]=new Color(1,1,1,Mathf.Clamp01(1-u/.4f));}Fill(gloss,px);
            // .storm-grid: repeating-linear-gradient(135deg, transparent 0 31px, line 32px 33px): a 1 px diagonal every 33 px.
            // A 46.67 px tile (33 * sqrt 2) holds two whole periods along the diagonal, so it repeats seamlessly.
            diagonal=Make(64,64,true);px=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float g=(x+(63-y)+1)*(DiagonalTile/64)/1.41421f,d=Mathf.Repeat(g,33);px[y*64+x]=new Color(1,1,1,Mathf.Clamp01(1-Mathf.Abs(d-32)/.7f));}Fill(diagonal,px);
        }
        // The legs: one metal strip per tier (pin-a → pin-b at 48% → pin-c, 7 px of every 15), tiled along the edge.
        private static Texture2D Pins(Style s)
        {
            Ensure();int i=Mathf.Clamp(s.Pins,0,pins.Length-1);if(pins[i]!=null)return pins[i];
            var t=Make(60,40,true);t.wrapModeV=TextureWrapMode.Clamp;var px=new Color[60*40];
            for(int y=0;y<40;y++){float v=1-(y+.5f)/40f;Color c=v<.48f?Color.Lerp(s.PinA,s.PinB,v/.48f):Color.Lerp(s.PinB,s.PinC,(v-.48f)/.52f);for(int x=0;x<60;x++){var p=c;p.a=x<28?1:0;px[y*60+x]=p;}}
            Fill(t,px);return pins[i]=t;
        }
        public static void Legs(Rect r,Style s,float inset,float height,float a)
        {
            var t=Pins(s);var top=new Rect(r.x+inset,r.y-height,r.width-2*inset,height);var bottom=new Rect(r.x+inset,r.yMax,r.width-2*inset,height);
            U.Tile(top,t,new Color(1,1,1,a),new Vector2(15,height));U.Tile(bottom,t,new Color(1,1,1,a),new Vector2(15,height),default,true);
        }
        // The substrate: linear-gradient(180deg, sub-a, sub-b) under the radius with the edge (Siren: the void-glass radial).
        public static void Substrate(Rect r,Style s,float radius,Color edge,float a)
        {
            U.Round(r,U.A(s.SubB,a),radius,U.A(edge,a));
            if(s.Void)
            {   // radial-gradient(130% 75% at 50% 112%, #3a2166, #160d27 42%, #07060b 72%) over #050409.
                var e=new Rect(r.center.x-1.3f*r.width,r.yMax+.12f*r.height-.75f*r.height,2.6f*r.width,1.5f*r.height);U.BeginBand(Inset(r,1.5f));
                U.RadialStops(e,U.Hex(0x07060b,a),Stops3,Alpha3);U.RadialStops(e,U.Hex(0x160d27,a),Stops3,Alpha2);U.RadialStops(e,U.Hex(0x3a2166,a),Stops2,Alpha1);U.EndBand();return;
            }
            float k=Mathf.Min(radius,r.height/2);U.Round(new Rect(r.x+1,r.y+1,r.width-2,2*k),U.A(s.SubA,a),k);U.Fade(new Rect(r.x+1,r.y+1+k,r.width-2,r.height-2-2*k),U.A(s.SubA,a));
        }
        private static readonly float[] Stops3={0,.42f,.72f,1},Alpha3={0,0,1,1},Alpha2={1,1,0,0},Stops2={0,.42f,1},Alpha1={1,0,0};
        private static Rect Inset(Rect r,float d)=>new Rect(r.x+d,r.y+d,r.width-2*d,r.height-2*d);
        // .chip-traces: the circuit traces anchored bottom-right (300 x 150 px) in the rarity colour at the tier's opacity.
        private const string TraceMarkup="<path d=\"M300 184H236l-18 18v98M300 200h-52l-14 14v86M300 216h-40l-10 10v74M300 262h-22l-10 10v28M196 300v-20l-12-12h-38M168 300v-12l-8-8h-30M300 238h-34\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/>"
            +"<circle cx=\"143\" cy=\"268\" r=\"3.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/><circle cx=\"127\" cy=\"280\" r=\"3.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/><circle cx=\"263\" cy=\"238\" r=\"3.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/>";
        public static void Traces(Rect r,Color rare,Style s,float a)=>NativeIcons.DrawMarkup(new Rect(r.xMax-299,r.yMax-299,300,300),"chip-traces",TraceMarkup,300,U.A(rare,s.Traces*a));
        // The heat spreader's brushed lines (#ffffff06 every 3 px) and the light sweep (hover, or the Legendary/Mythic gleam).
        public static void Brushed(Rect plate,float a){Ensure();U.Tile(new Rect(plate.x+1,plate.y+1,plate.width-2,plate.height-2),brush,new Color(1,1,1,.024f*a),new Vector2(3,1));}
        public static void Sweep(Rect plate,float u,float a)
        {
            float bw=plate.width*.38f,bx=plate.x+plate.width*(-1.3f+4.6f*u)*.38f;var band=Rect.MinMaxRect(Mathf.Max(plate.x,bx),plate.y,Mathf.Min(plate.xMax,bx+bw),plate.yMax);if(band.width<=0)return;
            // linear-gradient(100deg, transparent, #ffffff14 40%, #ffffff30 50%, transparent 62%).
            float x0=bx,w=bw;void Seg(float f0,float f1,float a0,float a1){var q=Rect.MinMaxRect(Mathf.Max(band.x,x0+w*f0),band.y,Mathf.Min(band.xMax,x0+w*f1),band.yMax);if(q.width>0)U.HGradient(q,new Color(1,1,1,a0*a),new Color(1,1,1,a1*a));}
            Seg(0,.4f,0,.078f);Seg(.4f,.5f,.078f,.19f);Seg(.5f,.62f,.19f,0);
        }
        // Mythic: the holographic foil drifting across the spreader over 7 s (115deg pink-gold-green-blue-violet, 300% wide).
        private static readonly Color[] Holo={U.Hex(0xff6475,.24f),U.Hex(0xffd36e,.19f),U.Hex(0x7dffb2,.17f),U.Hex(0x6ec8ff,.23f),U.Hex(0xc58bff,.24f),U.Hex(0xff6475,.24f)};
        public static void Holographic(Rect plate,float t,float a)
        {
            U.BeginBand(plate);float span=plate.width*3,x=plate.x-span*Mathf.Repeat(t/7,1);
            for(int rep=0;rep<2;rep++)for(int i=0;i<5;i++){var q=new Rect(x+rep*span+span*i/5f,plate.y,span/5f,plate.height);if(q.xMax<plate.x||q.x>plate.xMax)continue;U.HGradient(q,U.A(Holo[i],a),U.A(Holo[i+1],a));}
            U.EndBand();
        }
        // The die's glass: the grid (die 18% / 14%, .6 opacity, inset 6 px) and the 135deg gloss (#ffffff30 to clear at 40%).
        public static void DieGlass(Rect die,Color tint,float a)
        {
            Ensure();float inset=Mathf.Min(6,die.width*.12f);U.Tile(new Rect(die.x+inset,die.y+inset,die.width-2*inset,die.height-2*inset),dieGrid,U.A(tint,.108f*a),new Vector2(7,7));
            U.Image(die,gloss,new Color(1,1,1,.19f*a));
        }

        // ---- the exclusive kits' auras (z-index 1: over the substrate and particles, under the package's content) ----
        public static void Aura(Rect r,string tier,float t,float a,bool plain)
        {
            if(plain)return;
            switch(tier){case "mishima":Emperor(r,t,a);break;case "ansatsuken":Fighting(r,t,a);break;case "l44t":Rain(r,t,a);break;case "siren":Siren(r,t,a);break;}
        }
        // emperor-card.ts: a red current orbiting the package edge (conic, 3.2 s), the storm bolt, three current arcs and a grid.
        private static readonly Vector2[] Arc={new Vector2(0,.12f),new Vector2(.24f,.36f),new Vector2(.43f,.21f),new Vector2(.65f,.53f),new Vector2(.76f,.45f),new Vector2(1,.74f),new Vector2(.76f,.49f),new Vector2(.65f,.59f),new Vector2(.43f,.26f),new Vector2(.24f,.41f)};
        private static readonly Vector2[] ArcFlip={new Vector2(1,.88f),new Vector2(.76f,.64f),new Vector2(.57f,.79f),new Vector2(.35f,.47f),new Vector2(.24f,.55f),new Vector2(0,.26f),new Vector2(.24f,.51f),new Vector2(.35f,.41f),new Vector2(.57f,.74f),new Vector2(.76f,.59f)};
        private static readonly float[] Flat={0,1},Full={1,1};
        private static string bolt,seal,face,flames;
        private static void Emperor(Rect r,float t,float a)
        {
            BuildMarkup();U.BeginBand(r);
            Ensure();U.Tile(Inset(r,1),diagonal,U.Hex(0x90929e,.063f*a),new Vector2(DiagonalTile,DiagonalTile));
            NativeIcons.DrawMarkup(new Rect(r.xMax+16-150,r.yMax+22-165,150,150),"chip-storm-bolt",bolt,100,U.Hex(0xff1742,.09f*a));
            // .storm-arc: stormCurrent 2.6 s ease-in-out (.18 → .75 at 45-65% → .18), delays 0 / -1 / -1.8 s, with its drop-shadow.
            for(int i=0;i<3;i++)
            {
                float k=Mathf.Repeat(t+(i==0?0:i==1?1:1.8f),2.6f)/2.6f,e=k<.45f?Smooth(k/.45f):k<.65f?1:1-Smooth((k-.65f)/.35f),op=Mathf.Lerp(.18f,.75f,e)*a;
                float top=i==0?.8f:i==1?.88f:.84f,h=r.height*.22f*(i==2?.7f:1);var box=new Rect(r.x-r.width*.05f,r.y+r.height*top+(i==2?r.height*.22f*.15f:0),r.width*1.1f,h);
                var shape=i==1?ArcFlip:Arc;Color c=i==0?U.Hex(0xef2149):i==1?U.Hex(0xbd1235):U.Hex(0x080408);
                U.Polygon(new Rect(box.x,box.y-1.5f,box.width,box.height+3),shape,Flat,Full,U.Hex(0xfb254b,.35f*op));U.Polygon(box,shape,Flat,Full,U.A(c,op));
            }
            U.EndBand();
            // .emperor-aura:before: conic-gradient(from storm-angle, clear 0 60%, #ff244870 72%, #fff 77%, #ff2448 80%, clear 90%)
            // in a 2 px ring inside the border, the angle turning once every 3.2 s.
            float turn=360*Mathf.Repeat(t/3.2f,1);var ring=Inset(r,1);
            for(int side=0;side<4;side++)
            {
                bool horizontal=side%2==0;float len=horizontal?ring.width-12:ring.height-12;int n=Mathf.Max(1,Mathf.CeilToInt(len/5));float step=len/n;
                for(int i=0;i<n;i++)
                {
                    float along=6+(i+.5f)*step;Vector2 p=side==0?new Vector2(ring.x+along,ring.y+1):side==1?new Vector2(ring.xMax-1,ring.y+along):side==2?new Vector2(ring.xMax-along,ring.yMax-1):new Vector2(ring.x+1,ring.yMax-along);
                    float deg=Mathf.Atan2(p.x-ring.center.x,ring.center.y-p.y)*Mathf.Rad2Deg,u=Mathf.Repeat((deg-turn)/360,1);if(u<.6f||u>.9f)continue;
                    Color c=u<.72f?U.Hex(0xff2448,.44f*(u-.6f)/.12f):u<.77f?Color.Lerp(U.Hex(0xff2448,.44f),Color.white,(u-.72f)/.05f):u<.8f?Color.Lerp(Color.white,U.Hex(0xff2448),(u-.77f)/.03f):U.Hex(0xff2448,1-(u-.8f)/.1f);
                    U.Fill(horizontal?new Rect(p.x-step/2,p.y-1,step+.4f,2):new Rect(p.x-1,p.y-step/2,2,step+.4f),U.A(c,a));
                }
            }
        }
        // ansatsuken-card.ts: the ink seal, the red headband tied across the top, two ki rings orbiting and the dojo grid.
        private static readonly Vector2[] Band={new Vector2(0,.38f),new Vector2(1,0),new Vector2(1,.62f),new Vector2(0,1)};
        private static readonly float[] BandAtA={0,.35f,.5f,1},BandA={0,1,.2f,0},BandAtB={0,.35f,.5f,.8f,1},BandB={0,0,1,0,0},BandAtC={0,.5f,.8f,1},BandC={0,.3f,1,0};
        private static void Fighting(Rect r,float t,float a)
        {
            BuildMarkup();U.BeginBand(r);
            NativeIcons.DrawMarkup(new Rect(r.xMax+26-170,r.yMax+30-170,170,170),"chip-warrior-seal",seal,100,U.Hex(0xff294b,.1f*a));
            // .dojo-grid: the bottom 36%, a line every 16 px (#ff354e14) under perspective(90px) rotateX(25deg).
            float gh=r.height*.36f,cy=r.yMax-gh/2,cos=Mathf.Cos(25*Mathf.Deg2Rad),sin=Mathf.Sin(25*Mathf.Deg2Rad);
            for(float d=15.5f;d<gh;d+=16){float y=gh/2-d,z=y*sin,k=90/(90-z);float py=cy+y*cos*k,w=r.width*k;U.Fill(new Rect(r.center.x-w/2,py-.5f*k,w,Mathf.Max(.6f,k)),U.Hex(0xff354e,.08f*a));}
            // .ki-ring x2: 2 px #ff35554d circles (220 px and 180 px) with their left and bottom borders clear, turning (4 s, 3 s back).
            var centre=new Vector2(r.xMax-40,r.yMax-30);
            for(int ring=0;ring<2;ring++)
            {
                float radius=ring==0?110:90,rot=360*Mathf.Repeat(t/(ring==0?4:3f),1)*(ring==0?1:-1);int n=Mathf.CeilToInt(Mathf.PI*radius/2.6f);
                for(int i=0;i<=n;i++)
                {
                    float ang=(-135+180f*i/n+rot)*Mathf.Deg2Rad;var p=centre+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*(radius-1);if(p.x<r.x||p.x>r.xMax||p.y<r.y||p.y>r.yMax)continue;
                    float edge=Mathf.Clamp01(Mathf.Min(i,n-i)/(n*.06f));U.Fill(new Rect(p.x-1.1f,p.y-1.1f,2.2f,2.2f),U.Hex(0xff3555,.3f*edge*a));
                }
            }
            // .red-headband: transparent → #ff344f 35% → #ff999d 50% → #b41634 80% → transparent, 120% wide, tilted, flowing (2.8 s).
            float k2=Mathf.PingPong(t/1.4f,1),flow=Smooth(k2),tilt=Mathf.Tan(2.5f*Mathf.Deg2Rad)*r.width*1.2f;var band=new Rect(r.x-r.width*.1f+8*flow,r.y+3-tilt/2,r.width*1.2f,8+tilt);float op=(.8f+.2f*flow)*a;
            U.Polygon(band,Band,BandAtA,BandA,U.Hex(0xff344f,op));U.Polygon(band,Band,BandAtC,BandC,U.Hex(0xb41634,op));U.Polygon(band,Band,BandAtB,BandB,U.Hex(0xff999d,op));
            U.EndBand();
        }
        // matrixRain(8): eight columns of 1s and 0s (15 px mono, line 21 px, #5eff91 at .3) falling over 4 s, faded at both ends.
        private static readonly string[] Bit={"0","1"};
        private static void Rain(Rect r,float t,float a)
        {
            const float line=21,column=32*line;U.BeginBand(r);
            for(int i=0;i<8;i++)
            {
                float phase=Mathf.Repeat((t+(i%7)*.37f)/4,1),top=r.y-r.height+2.1f*column*phase,x=r.x+r.width*i*.125f;
                int first=Mathf.Max(0,Mathf.FloorToInt((r.y-top)/line)),last=Mathf.Min(31,Mathf.CeilToInt((r.yMax-top)/line));
                for(int n=first;n<=last;n++)
                {
                    float y=top+n*line,f=(y+line/2-r.y)/r.height,mask=f<.2f?f/.2f:f>.8f?(1-f)/.2f:1;if(mask<=.02f)continue;
                    U.Text(new Rect(x,y,12,line),Bit[(i*7+n*13)%5<2?1:0],15,U.Hex(0x5eff91,.3f*Mathf.Clamp01(mask)*a),TextAnchor.MiddleLeft,mono:true);
                }
            }
            U.EndBand();
        }
        // siren-card.ts sirenAura: black flame tongues licking up the bottom, the grin watching from the fire, scanlines, and on
        // the glitch beat (65-72% of 4.2 s) the grin flickers and a tear crosses the package.
        private static void Siren(Rect r,float t,float a)
        {
            BuildMarkup();Ensure();float g=Mathf.Repeat(t,4.2f)/4.2f;U.BeginBand(Inset(r,1));
            // .siren-flames: left -4%, width 108%, the bottom 42%, its 200 x 70 view box sliced to the box's width (xMidYMax).
            // The tongues' siren-lick, as one slow breath of the row's height (no transform inside the clip). It only ever shrinks the
            // drawn height, so the raster size (the width) and its cached texture stay the same every frame.
            var box=new Rect(r.x-r.width*.04f,r.yMax-r.height*.42f+2,r.width*1.08f,r.height*.42f);float s=Mathf.Max(box.width/200,box.height/70),lick=.95f+.05f*Mathf.Sin(t*2*Mathf.PI/1.7f);
            U.BeginBand(box);NativeIcons.DrawMarkup(new Rect(box.center.x-100*s,box.yMax-200*s*lick,200*s,200*s*lick),"chip-siren-flames",flames,200,new Color(1,1,1,a),320);U.EndBand();
            // .siren-face: 70% wide at right -6%, bottom 2%, turned -6deg; siren-watch flickers .16 → .42 / .03 / .32 on the beat.
            float watch=g>=.65f&&g<.66f?.42f:g>=.66f&&g<.68f?.03f:g>=.68f&&g<.72f?.32f:.16f,fw=r.width*.7f;
            NativeIcons.DrawMarkup(new Rect(r.xMax+r.width*.06f-fw,r.yMax-r.height*.02f-fw*.6f,fw,fw),"chip-siren-face",face,200,new Color(1,1,1,watch*a),320);
            U.Tile(r,scan,new Color(1,1,1,.031f*a),new Vector2(1,3));
            // .siren-tear: a 12 px band (transparent → #b7a8ff40 20% → #f6f3ff70 50% → #2de2ff38 72% → transparent) on the beat.
            if(g>=.65f&&g<.72f)
            {
                int step=g<.67f?0:g<.69f?1:2;float y=r.y+r.height*(step==0?.22f:step==1?.61f:.44f),dx=step==0?-8:step==1?10:-5,op=(step==1?.8f:1)*a,x0=r.x-r.width*.02f+dx,w=r.width*1.04f;
                U.HGradient(new Rect(x0,y,w*.2f,12),Color.clear,U.Hex(0xb7a8ff,.25f*op));U.HGradient(new Rect(x0+w*.2f,y,w*.3f,12),U.Hex(0xb7a8ff,.25f*op),U.Hex(0xf6f3ff,.44f*op));
                U.HGradient(new Rect(x0+w*.5f,y,w*.22f,12),U.Hex(0xf6f3ff,.44f*op),U.Hex(0x2de2ff,.22f*op));U.HGradient(new Rect(x0+w*.72f,y,w*.28f,12),U.Hex(0x2de2ff,.22f*op),Color.clear);
            }
            U.EndBand();
        }
        // siren-jolt (4.2 s, steps): the header's shake on the glitch beat, px.
        public static float Jolt(float t){float g=Mathf.Repeat(t,4.2f)/4.2f;return g<.65f||g>=.72f?0:g<.66f?-4:g<.67f?5:g<.69f?-2:g<.70f?3:-1;}
        private static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}

        // ---- markup (built once): the bolt and the seal turned as the CSS turns them, the face and the card's flame row ----
        private static void BuildMarkup()
        {
            if(bolt!=null)return;
            bolt="<path d=\""+Turn("M60 4L18 54L43 54L33 96L86 37L57 37Z",12,50,50,1,1,0,0)+"\" fill=\"#fff\"/>";
            seal="<path d=\""+Turn("M26 23L76 19L74 28L25 31ZM13 42L89 36L86 46L12 51ZM48 29L58 28Q57 76 18 94L10 86Q47 67 48 29ZM56 48Q68 70 92 81L88 94Q61 83 50 60Z",-10,50,50,1,1,0,0)+"\" fill=\"#fff\"/>";
            var f=new StringBuilder();
            foreach(var part in System.Text.RegularExpressions.Regex.Split(ConversionGame.SirenFace,"(?<=/>)"))
            {
                var m=System.Text.RegularExpressions.Regex.Match(part,"d=\"([^\"]*)\"");if(!m.Success){f.Append(part);continue;}
                f.Append(part.Substring(0,m.Groups[1].Index)).Append(Turn(m.Groups[1].Value,-6,100,60,1,1,0,0)).Append(part.Substring(m.Groups[1].Index+m.Groups[1].Length));
            }
            face=f.ToString();
            // flames(200,70,CARD_FRONT,CARD_BACK): back tongues (rim only), then front tongues with their black core (.84 x .86),
            // standing on y = 70 of a 200 x 70 box, here on the bottom of a 200 square (y + 130).
            const string Right="M-10 0 C-14 -12 -9 -22 -5 -32 C-2 -40 -3 -50 0 -60 C7 -50 10 -42 8 -32 C12 -36 13 -42 12 -47 C19 -36 19 -16 10 0 Z",Left="M10 0 C14 -12 9 -22 5 -32 C2 -40 3 -50 0 -60 C-7 -50 -10 -42 -8 -32 C-12 -36 -13 -42 -12 -47 C-19 -36 -19 -16 -10 0 Z";
            float[,] front={{10,.62f},{38,.9f},{66,.7f},{94,1.05f},{124,.78f},{152,.95f},{182,.68f}},back={{0,.5f},{24,.6f},{52,.48f},{80,.66f},{110,.55f},{138,.62f},{166,.5f},{196,.58f}};
            var fl=new StringBuilder();
            for(int i=0;i<back.GetLength(0);i++)fl.Append("<path d=\"").Append(Turn(i%2==1?Left:Right,0,0,0,back[i,1],back[i,1],back[i,0],200)).Append("\" fill=\"#150d2c\" stroke=\"#2b2448\" stroke-width=\"1\"/>");
            for(int i=0;i<front.GetLength(0);i++){string d=i%2==1?Left:Right;float sc=front[i,1];
                fl.Append("<path d=\"").Append(Turn(d,0,0,0,sc,sc,front[i,0],200)).Append("\" fill=\"#3b2786\" stroke=\"#a99fe0\" stroke-width=\"1\"/>");
                fl.Append("<path d=\"").Append(Turn(d,0,0,0,sc*.84f,sc*.86f,front[i,0],200)).Append("\" fill=\"#030205\"/>");}
            flames=fl.ToString();
        }
        // Absolute path data with every coordinate pair scaled (sx, sy), turned `degrees` clockwise about (cx, cy) and moved.
        private static string Turn(string d,float degrees,float cx,float cy,float sx,float sy,float tx,float ty)
        {
            float c=Mathf.Cos(degrees*Mathf.Deg2Rad),s=Mathf.Sin(degrees*Mathf.Deg2Rad);var o=new StringBuilder();var nums=System.Text.RegularExpressions.Regex.Matches(d,@"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+)");
            bool haveX=false;float x=0;
            foreach(System.Text.RegularExpressions.Match m in nums)
            {
                if(char.IsLetter(m.Value[0])){o.Append(m.Value);continue;}
                float v=float.Parse(m.Value,CultureInfo.InvariantCulture);if(!haveX){x=v;haveX=true;continue;}haveX=false;
                float px=x*sx-cx,py=v*sy-cy,rx=px*c-py*s+cx+tx,ry=px*s+py*c+cy+ty;o.Append(rx.ToString("0.###",CultureInfo.InvariantCulture)).Append(' ').Append(ry.ToString("0.###",CultureInfo.InvariantCulture)).Append(' ');
            }
            return o.ToString();
        }
    }
}
