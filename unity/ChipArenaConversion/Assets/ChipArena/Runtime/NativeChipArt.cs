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
    // tear; and the draft tray's holo scan highlight (lit edge and comet, powered legs, brackets, scan line, halo; the pick's
    // lock). Textures are built once on first use (never in field initializers); a frame allocates nothing.
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

        // ---- the holo scan: the draft tray's highlighted card (NativeHoloScan's timeline; NativeChipCard, NativeDraftScreen) ----
        // A tier-lit edge traced round the package's border both ways from the top-left corner, then one comet a lap; the legs
        // powering up in the tier's pin colour as the trace passes them (brighter as the comet runs by); corner brackets that
        // snap in outside the card over a dark keyline (so they read over the pilot's light torso); one scan line down the face
        // under the text, over faint 3 px holo lines; and a halo of the tier's light behind the package (the die's charge is
        // NativeChipCard's). `k` is the level times the card's alpha, `age` seconds since it engaged, `t` the screen clock. Calm
        // (Reduced effects) keeps the still parts. Every line is at least one physical pixel; nothing allocates.
        private struct HoloTier {public Color Edge,Head,Halo,Bracket,Tick,Scan,Pin;public float HaloAlpha,BracketGlow;public Color[] Tail;public bool Glitch,Blink,Foil;}
        private static readonly Color[] GoldTail={U.Hex(0xfff2cb),U.Hex(0xffc96d),U.Hex(0xf39a35)},FoilTail={U.Hex(0xffb3bd),U.Hex(0xffe39a),U.Hex(0xa8ffd0),U.Hex(0x9fd8ff),U.Hex(0xe2b6ff)};
        private static readonly Color GlitchCyan=U.Hex(0x2de2ff),Keyline=U.Hex(0x05060a);
        // Per tier: the edge line, the comet head (Legendary's gold and Mythic's foil tail), the halo and its strength, the brackets
        // (Mythic's four foil corners) with their glow and corner ticks, the scan and the lit legs; L44T's brackets blink once as
        // they boot, Siren's edge and brackets jolt on the card's glitch beat.
        private static HoloTier HoloOf(string tier,Color rare)
        {
            var h=new HoloTier{Edge=U.Mix(rare,Color.white,.18f),Head=U.Mix(rare,Color.white,.62f),Halo=rare,Bracket=U.Mix(rare,Color.white,.38f),Tick=U.Mix(rare,Color.white,.75f),Scan=U.Mix(rare,Color.white,.5f),Pin=U.Mix(rare,Color.white,.45f),HaloAlpha=.36f,BracketGlow=.3f};
            switch(tier)
            {
                case "common":h.Edge=U.Hex(0xdfe4ec);h.Head=Color.white;h.Halo=U.Hex(0xe7ebf2);h.HaloAlpha=.2f;h.Bracket=U.Hex(0xf1f3f7);h.Tick=Color.white;h.Scan=Color.white;h.Pin=U.Hex(0xf1f5f9);h.BracketGlow=.2f;break;
                case "legendary":h.Edge=U.Hex(0xffc96d);h.Head=U.Hex(0xfff2cb);h.Halo=U.Hex(0xffae55);h.Bracket=U.Hex(0xfff4c9);h.Tick=U.Hex(0xfff2cb);h.Scan=U.Hex(0xffe3a8);h.Pin=U.Hex(0xffe3a8);h.Tail=GoldTail;break;
                case "mythic":h.Edge=U.Hex(0xff8a96);h.Head=U.Hex(0xfff4f6);h.Halo=U.Hex(0xff6475);h.Bracket=U.Hex(0xffe39a);h.Tick=Color.white;h.Scan=U.Hex(0xffeef0);h.Pin=U.Hex(0xffe39a);h.Tail=FoilTail;h.Foil=true;break;
                case "mishima":h.Edge=U.Hex(0xb3b5c2);h.Head=U.Hex(0xff2448);h.Halo=U.Hex(0xff173b);h.HaloAlpha=.26f;h.Bracket=U.Hex(0xd9dbe3);h.Tick=U.Hex(0xff2448);h.Scan=U.Hex(0xff6a83);h.Pin=U.Hex(0xff6a83);break;
                case "ansatsuken":h.Edge=U.Hex(0xff354e);h.Head=U.Hex(0xffb2bc);h.Halo=U.Hex(0xff214b);h.Bracket=U.Hex(0xffd6da);h.Tick=U.Hex(0xffb2bc);h.Scan=U.Hex(0xff8a97);h.Pin=U.Hex(0xff8a97);break;
                case "l44t":h.Edge=U.Hex(0x3fd979);h.Head=U.Hex(0xd4ffe2);h.Halo=U.Hex(0x36ff78);h.Bracket=U.Hex(0x9effbd);h.Tick=U.Hex(0xd4ffe2);h.Scan=U.Hex(0x5eff91);h.Pin=U.Hex(0xa0ffc0);h.Blink=true;break;
                case "siren":h.Edge=U.Hex(0x9b87ff);h.Head=U.Hex(0xf3efff);h.Halo=U.Hex(0x8f73ff);h.HaloAlpha=.38f;h.Bracket=U.Hex(0xc9bfff);h.Tick=U.Hex(0xf3efff);h.Scan=U.Hex(0xb7a8ff);h.Pin=U.Hex(0xc9bfff);h.Glitch=true;break;
            }
            return h;
        }
        private static float Px(float v)=>Mathf.Max(1,v*U.S)/U.S;
        // The package's border as U.Round draws it at `radius`: its corner radius class (4/8/12/16 physical px) and the 1.5 px
        // line, so the traced stretches and the comet meet the finished ring exactly.
        private static void EdgeOf(float radius,out float R,out float w){int i=Mathf.Clamp(Mathf.RoundToInt(radius*U.S/4)-1,0,3);R=(4+i*4)/U.S;w=Px(1.5f);}
        private static Color Cycle(float u,Color[] c){float x=Mathf.Repeat(u,1)*c.Length;int i=(int)x%c.Length;return Color.Lerp(c[i],c[(i+1)%c.Length],x-Mathf.Floor(x));}
        // Behind the package: the halo of the tier's light (breathing, NativeHoloScan.Swell) and a tight glow of its edge.
        public static void HoloHalo(Rect r,string tier,Color rare,float k,float t,bool calm)
        {
            var h=HoloOf(tier,rare);U.Glow(r,U.A(h.Halo,h.HaloAlpha*k*NativeHoloScan.Swell(t,calm)),40);U.Glow(r,U.A(h.Edge,.16f*k),10);
        }
        // On the package's border: the trace, then the finished ring (Mythic's drifting foil) and the comet with its bead.
        public static void HoloEdge(Rect r,float radius,string tier,Color rare,float k,float age,float t,bool calm)
        {
            var h=HoloOf(tier,rare);EdgeOf(radius,out float R,out float w);var q=r;float L=Perimeter(q,R,w),start=L-Mathf.PI*(R-w/2)/4,ring=.34f*k;
            // Siren: the edge jolts with the spreader on the glitch beat, a cyan copy on the other side.
            float jolt=h.Glitch&&!calm?Jolt(t)*.75f:0;if(jolt!=0){U.Round(new Rect(q.x-jolt,q.y,q.width,q.height),Color.clear,radius,U.A(GlitchCyan,.35f*k),1.5f);q.x+=jolt;}
            Color edge=h.Foil&&!calm?Cycle(t/7,FoilTail):h.Edge;float reach=NativeHoloScan.TraceReach(age,calm);
            if(reach>=1)U.Round(q,Color.clear,radius,U.A(edge,ring),1.5f);
            else
            {   // The trace: both ways from the middle of the top-left arc, its leading tenth ramping up to the head, a bead on each end.
                float half=reach*L/2,lead=Mathf.Min(half,L*.1f);Stretch(q,R,w,start-half,start+half,ring,ring,edge);
                Stretch(q,R,w,start+half-lead,start+half,0,.9f*k,h.Head);Stretch(q,R,w,start-half,start-half+lead,.9f*k,0,h.Head);Bead(Along(q,R,w,start+half),h.Head,k);Bead(Along(q,R,w,start-half),h.Head,k);
            }
            // The comet: from where the trace closed, clockwise once a lap, its tail (twice the line) growing in; Legendary's gold
            // and Mythic's foil in bands.
            if(!NativeHoloScan.Comet(age,calm,out float head,out float tail))return;float hd=start+head*L,len=tail*L+.01f;
            if(h.Tail==null)Stretch(q,R,2*w,hd-len,hd,0,k,h.Head);
            else{int n=h.Tail.Length;for(int s=0;s<n;s++)Stretch(q,R,2*w,hd-len*(s+1f)/n,hd-len*s/(float)n,k*(1-(s+1f)/n),k*(1-s/(float)n),h.Tail[s]);}
            Bead(Along(q,R,w,hd),h.Head,k);
        }
        // A round white-hot bead in a glow of the head colour.
        private static void Bead(Vector2 p,Color head,float k){U.Glow(new Rect(p.x-2,p.y-2,4,4),U.A(head,.7f*k),8);U.Glow(new Rect(p.x-.8f,p.y-.8f,1.6f,1.6f),U.A(Color.Lerp(head,Color.white,.6f),Mathf.Min(1,k)),2.2f);}
        // The outline's centre line, clockwise from the top run's left end: top, top-right arc, right, ..., top-left arc.
        private static float Perimeter(Rect q,float R,float w)=>2*(q.width-2*R)+2*(q.height-2*R)+Mathf.PI*(R-w/2)*2;
        private static Vector2 ArcPoint(Vector2 centre,float rc,int corner,float f){float a=(corner+f)*.5f*Mathf.PI;return centre+new Vector2(Mathf.Sin(a),-Mathf.Cos(a))*rc;}
        private static Vector2 Centre(Rect q,float R,int corner)=>corner==0?new Vector2(q.xMax-R,q.y+R):corner==1?new Vector2(q.xMax-R,q.yMax-R):corner==2?new Vector2(q.x+R,q.yMax-R):new Vector2(q.x+R,q.y+R);
        private static Vector2 Along(Rect q,float R,float w,float d)
        {
            float sw=q.width-2*R,sh=q.height-2*R,rc=R-w/2,arc=Mathf.PI*rc/2,o=w/2;d=Mathf.Repeat(d,Perimeter(q,R,w));
            if(d<sw)return new Vector2(q.x+R+d,q.y+o);d-=sw;if(d<arc)return ArcPoint(Centre(q,R,0),rc,0,d/arc);d-=arc;
            if(d<sh)return new Vector2(q.xMax-o,q.y+R+d);d-=sh;if(d<arc)return ArcPoint(Centre(q,R,1),rc,1,d/arc);d-=arc;
            if(d<sw)return new Vector2(q.xMax-R-d,q.yMax-o);d-=sw;if(d<arc)return ArcPoint(Centre(q,R,2),rc,2,d/arc);d-=arc;
            if(d<sh)return new Vector2(q.x+o,q.yMax-R-d);d-=sh;return ArcPoint(Centre(q,R,3),rc,3,Mathf.Clamp01(d/arc));
        }
        // The outline from d0 to d1 (any range, wrapping) at alpha a0 → a1 along it, `w` thick inside the border.
        private static void Stretch(Rect q,float R,float w,float d0,float d1,float a0,float a1,Color c)
        {
            float L=Perimeter(q,R,w);if(d1-d0<=.01f||L<=0)return;if(d1-d0>L)d0=d1-L;float shift=Mathf.Floor(d0/L)*L;d0-=shift;d1-=shift;
            if(d1<=L){Runs(q,R,w,d0,d1,a0,a1,c);return;}float am=Mathf.Lerp(a0,a1,(L-d0)/(d1-d0));Runs(q,R,w,d0,L,a0,am,c);Runs(q,R,w,0,d1-L,am,a1,c);
        }
        private static void Runs(Rect q,float R,float w,float d0,float d1,float a0,float a1,Color c)
        {
            float sw=q.width-2*R,sh=q.height-2*R,arc=Mathf.PI*(R-w/2)/2,at=0;
            for(int p=0;p<8;p++)
            {
                float len=p%2==1?arc:p%4==0?sw:sh,s0=Mathf.Max(d0,at),s1=Mathf.Min(d1,at+len);
                if(s1>s0&&len>0)
                {
                    float b0=Mathf.Lerp(a0,a1,(s0-d0)/(d1-d0)),b1=Mathf.Lerp(a0,a1,(s1-d0)/(d1-d0)),u0=s0-at,u1=s1-at;
                    switch(p)
                    {
                        case 0:Ramp(new Rect(q.x+R+u0,q.y,u1-u0,w),true,b0,b1,c);break;
                        case 2:Ramp(new Rect(q.xMax-w,q.y+R+u0,w,u1-u0),false,b0,b1,c);break;
                        case 4:Ramp(new Rect(q.xMax-R-u1,q.yMax-w,u1-u0,w),true,b1,b0,c);break;
                        case 6:Ramp(new Rect(q.x,q.yMax-R-u1,w,u1-u0),false,b1,b0,c);break;
                        default:Corner(q,R,w,p/2,u0/len,u1/len,b0,b1,c);break;
                    }
                }
                at+=len;
            }
        }
        // A straight run lit `first` at its left / top end and `last` at the other: the lower alpha flat, the rest a ramp.
        private static void Ramp(Rect q,bool horizontal,float first,float last,Color c)
        {
            float lo=Mathf.Min(first,last),d=Mathf.Abs(last-first);if(lo>.002f)U.Fill(q,U.A(c,lo));if(d<.004f)return;
            if(horizontal)U.HGradient(q,last>first?Color.clear:U.A(c,d),last>first?U.A(c,d):Color.clear);else U.Fade(q,U.A(c,d),first>last);
        }
        // A stretch f0..f1 of a quarter arc: U.Round's own ring clipped to the stretch's box (x and y are monotone inside a
        // quadrant, so the box holds that stretch alone), in up to three alpha steps.
        private static void Corner(Rect q,float R,float w,int corner,float f0,float f1,float b0,float b1,Color c)
        {
            var centre=Centre(q,R,corner);int steps=Mathf.Max(1,Mathf.CeilToInt((f1-f0)*3-.001f));float pad=.35f/U.S;
            for(int s=0;s<steps;s++)
            {
                float g0=Mathf.Lerp(f0,f1,(float)s/steps),g1=Mathf.Lerp(f0,f1,(s+1f)/steps),al=Mathf.Lerp(b0,b1,(s+.5f)/steps);if(al<=.002f)continue;
                Vector2 a=ArcPoint(centre,R,corner,g0),b=ArcPoint(centre,R,corner,g1),e=ArcPoint(centre,R-w,corner,g0),f=ArcPoint(centre,R-w,corner,g1);
                U.BeginBand(Rect.MinMaxRect(Mathf.Min(Mathf.Min(a.x,b.x),Mathf.Min(e.x,f.x))-pad,Mathf.Min(Mathf.Min(a.y,b.y),Mathf.Min(e.y,f.y))-pad,Mathf.Max(Mathf.Max(a.x,b.x),Mathf.Max(e.x,f.x))+pad,Mathf.Max(Mathf.Max(a.y,b.y),Mathf.Max(e.y,f.y))+pad));
                U.Round(q,Color.clear,R,U.A(c,al),w);U.EndBand();
            }
        }
        // The legs power up in the tier's pin colour as the trace passes them (NativeHoloScan.LegLit; calm: all lit, still), each
        // row over a soft glow as long as its lit legs, a leg brighter as the comet runs by; the tips stay bare metal.
        public static void HoloLegs(Rect r,float radius,float inset,float pin,string tier,Color rare,float k,float age,bool calm)
        {
            var h=HoloOf(tier,rare);EdgeOf(radius,out float R,out float w);float span=r.width-2*inset,reach=NativeHoloScan.TraceReach(age,calm),L=Perimeter(r,R,w),arc=Mathf.PI*(R-w/2)/2,sw=r.width-2*R,sh=r.height-2*R;
            bool comet=NativeHoloScan.Comet(age,calm,out float head,out _);float hc=Mathf.Repeat(L-arc/2+head*L,L);Color hot=Color.Lerp(h.Pin,Color.white,.35f);
            for(int row=0;row<2;row++)
            {
                bool bottom=row==1,any=false;float sum=0,x0=0,x1=0,y=bottom?r.yMax:r.y-pin;int count=0;
                for(float o=0;o<span-.01f;o+=15){float lw=Mathf.Min(7,span-o),lit=NativeHoloScan.LegLit(reach,r.width,r.height,R,inset+o+lw/2,bottom);sum+=lit;count++;if(lit>.02f){if(!any)x0=r.x+inset+o;x1=r.x+inset+o+lw;any=true;}}
                if(!any)continue;U.Glow(new Rect(x0,y+pin/2-1,x1-x0,2),U.A(h.Pin,.18f*k*sum/count),5);
                for(float o=0;o<span-.01f;o+=15)
                {
                    float lw=Mathf.Min(7,span-o),cx=inset+o+lw/2,lit=NativeHoloScan.LegLit(reach,r.width,r.height,R,cx,bottom);if(lit<=.02f)continue;
                    float bump=0;if(comet){float s=bottom?sw+arc+sh+arc+(r.width-R-cx):cx-R,d=Mathf.Repeat(s-hc,L);bump=Mathf.Max(0,1-Mathf.Min(d,L-d)/12);}
                    U.Fill(new Rect(r.x+inset+o,bottom?y:y+1,lw,pin-1),U.A(hot,Mathf.Min(1,(.32f*lit+.4f*bump)*k)));
                }
            }
        }
        // The scan: faint 3 px holo lines while lit (Siren has its own) and the line's one pass down the face, a trailing sheen
        // above it, clipped to the face; calm: a still sheen down from the top edge instead of the pass.
        public static void HoloScan(Rect r,string tier,Color rare,float k,float age,bool calm)
        {
            var h=HoloOf(tier,rare);Ensure();var face=Inset(r,1.5f);if(!h.Glitch)U.Tile(face,scan,U.A(h.Scan,.03f*k),new Vector2(1,3));
            if(calm){U.Fade(new Rect(face.x,face.y,face.width,Mathf.Min(60,face.height*.3f)),U.A(h.Scan,.05f*k));return;}
            if(!NativeHoloScan.ScanPass(age,calm,out float e))return;float y=face.y-14+(face.height+28)*e,lw=face.width,th=Px(1);
            U.BeginBand(face);U.Fade(new Rect(face.x,y-34,lw,34),U.A(h.Scan,.1f*k),false);U.Fade(new Rect(face.x,y+th,lw,8),U.A(h.Scan,.05f*k));
            U.HGradient(new Rect(face.x,y,lw*.25f,th),Color.clear,U.A(h.Head,.26f*k));U.Fill(new Rect(face.x+lw*.25f,y,lw*.5f,th),U.A(h.Head,.26f*k));U.HGradient(new Rect(face.x+lw*.75f,y,lw*.25f,th),U.A(h.Head,.26f*k),Color.clear);U.EndBand();
        }
        // The corner brackets outside the card: they snap in on engage (NativeHoloScan.BracketOffset) and drift out as the level
        // falls (the held card's rest further out); Siren's jolt on the glitch beat with a cyan copy. `a` the card's alpha.
        public static void HoloBrackets(Rect r,string tier,Color rare,float level,float a,float age,float t,bool calm)
        {
            var h=HoloOf(tier,rare);Brackets(r,h,NativeHoloScan.BracketOffset(age,level,calm),level*a*NativeHoloScan.BracketOpacity(age,calm,h.Blink),h.Glitch&&!calm?Jolt(t)*.75f:0,true);
        }
        // The keyboard focus while the pointer is on another card: still brackets at rest, no glow (`a` the card's alpha).
        public static void HoloGhost(Rect r,string tier,Color rare,float a){if(a>0)Brackets(r,HoloOf(tier,rare),NativeHoloScan.BracketRest,NativeHoloScan.Ghost*a,0,false);}
        // Four square-ended L brackets `off` px outside the card's corners (the arm clear of the legs), each over a dark keyline,
        // with a soft glow and a corner tick; Mythic's in four foil colours.
        private static void Brackets(Rect r,HoloTier h,float off,float op,float dx,bool glow)
        {
            if(op<=.002f)return;op=Mathf.Min(1,op);const float th=2,tick=3.5f;float ax=Mathf.Min(18,r.width*.14f),ay=Mathf.Min(26,r.height*.16f),key=Px(1);
            for(int c=0;c<4;c++)
            {
                bool right=c==1||c==2,bottom=c>=2;float px=(right?r.xMax+off:r.x-off)+dx,py=bottom?r.yMax+off:r.y-off;Color col=h.Foil?FoilTail[c]:h.Bracket;
                // The arm along the edge and the leg down the side (not overlapping), the tick over their corner.
                var arm=new Rect(right?px-ax:px,bottom?py-th:py,ax,th);var leg=new Rect(right?px-th:px,bottom?py-ay:py+th,th,ay-th);
                U.Fill(new Rect(arm.x-key,arm.y-key,arm.width+2*key,arm.height+2*key),U.A(Keyline,.55f*op));U.Fill(new Rect(leg.x-key,bottom?leg.y-key:leg.y+key,leg.width+2*key,leg.height),U.A(Keyline,.55f*op));
                if(dx!=0){U.Fill(new Rect(arm.x-2*dx,arm.y,arm.width,arm.height),U.A(GlitchCyan,.45f*op));U.Fill(new Rect(leg.x-2*dx,leg.y,leg.width,leg.height),U.A(GlitchCyan,.45f*op));}
                if(glow){U.Glow(arm,U.A(col,h.BracketGlow*op),5);U.Glow(leg,U.A(col,h.BracketGlow*op),5);}
                U.Fill(arm,U.A(col,op));U.Fill(leg,U.A(col,op));U.Fill(new Rect(right?px-tick:px,bottom?py-tick:py,tick,tick),U.A(h.Tick,op));
            }
        }
        // The pick (NativeDraftScreen.FlyToPalm, inside the fly's transform; no clips, so safe under its rotation): the brackets
        // clamp onto the card and its edge flares in the head colour over NativeHoloScan.Lock s with a faint light across the
        // face (the die's flare is NativeChipCard's); calm: the brackets stay at rest and fade, a softer ring, no light.
        public static void HoloLock(Rect r,float radius,string tier,Color rare,float age,bool calm,float a)
        {
            if(age<0||age>=NativeHoloScan.Lock||a<=0)return;var h=HoloOf(tier,rare);float u=age/NativeHoloScan.Lock,op=(1-u)*a;
            Brackets(r,h,NativeHoloScan.LockOffset(age,calm),op,0,true);U.Round(r,Color.clear,radius,U.A(h.Head,(calm?.5f:.9f)*op),1.5f);if(!calm)U.Round(r,U.A(Color.white,.14f*(1-u)*(1-u)*a),radius);
        }

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
