using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The chip card's design layers, drawn by NativeChipCard: a chamfered processor module in the cyber HUD's language (the
    // top-left and bottom-right corners cut as NativeHudChrome cuts its panels; the top-left cut is the package's pin-1 mark).
    // Per tier: the package materials (legs, body, edge, heat spreader, die, label and name colours, the tier glow); the body's
    // micro-texture, light bleed and circuit traces; the frame (crisp edge, bevel lines, the tier's accent runs and corner
    // marks); the anodised, brushed spreader and its gleam; the die's socket, glass and grid. The exclusive kits' card art:
    // Emperor's storm orbit, bolt, current arcs and grid; Ansatsuken's seal, headband, ki rings and dojo grid; L44T's binary
    // rain; Siren's void glass, black flames, watching grin, scanlines and tear. And the draft tray's holo scan highlight (lit
    // edge and comet round the chamfered outline, powered legs, brackets, scan line, halo; the pick's lock). Textures are built
    // once on first use (never in field initializers); a frame allocates nothing.
    public static class NativeChipArt
    {
        public struct Style
        {
            public Color PinA,PinB,PinC,SubA,SubB,PlateA,PlateB,Edge,Die,Name,Label,Glow,Spark,SparkGlow,Accent;
            public float GlowSpread,Traces,Tracking;public bool LabelGlow,Void;public int Pins;
        }
        // Per tier: the leg metal (pin-a/b/c), the body (sub-a/b: a navy black lit faintly by the tier), the anodised spreader
        // (plate-a/b), the edge, the die, the name and label colours, the label tracking, the frame's accent and the tier glow.
        public static Style For(string tier,Color rare)
        {
            var s=new Style{PinA=U.Hex(0xf1f3f7),PinB=U.Hex(0xa1a6b1),PinC=U.Hex(0x4c505a),SubA=U.Mix(U.Hex(0x161c2b),rare,.06f),SubB=U.Hex(0x0a0d15),PlateA=U.Mix(U.Hex(0x2a3245),rare,.14f),PlateB=U.Mix(U.Hex(0x131824),rare,.05f),
                Edge=U.Mix(U.Hex(0x2a3a6b),rare,.5f),Die=rare,Name=U.Hex(0xf3f6fb),Label=U.Mix(Color.white,rare,.82f),Spark=rare,SparkGlow=rare,Accent=rare,Traces=.1f,Tracking=.12f,Pins=0};
            switch(tier)
            {
                case "common":s.Die=U.Hex(0xe7ebf2);s.Edge=U.Hex(0x4a5672);s.Accent=U.Hex(0xc9d4e6);s.PlateA=U.Hex(0x2f3647);break;
                case "epic":s.SubA=U.Hex(0x1a172b);s.Traces=.17f;s.Glow=U.Hex(0xce8aff,.15f);s.GlowSpread=18;break;
                case "legendary":s.PinA=U.Hex(0xfff4c9);s.PinB=U.Hex(0xe3ad4a);s.PinC=U.Hex(0x7b5413);s.PlateA=U.Hex(0x4d3b17);s.PlateB=U.Hex(0x20180a);s.SubA=U.Hex(0x1c170f);s.SubB=U.Hex(0x0e0c08);s.Edge=U.Mix(U.Hex(0x3a2e18),rare,.62f);s.Name=U.Hex(0xffd28c);s.Accent=U.Hex(0xffc96d);s.Traces=.2f;s.Glow=U.Hex(0xffae55,.21f);s.GlowSpread=24;s.Pins=1;break;
                case "mythic":s.PinA=U.Hex(0xfff4c9);s.PinB=U.Hex(0xe3ad4a);s.PinC=U.Hex(0x7b5413);s.PlateA=U.Hex(0x2e2532);s.PlateB=U.Hex(0x15111a);s.SubA=U.Hex(0x1d1520);s.SubB=U.Hex(0x0e0a0d);s.Edge=U.Mix(U.Hex(0x3a2430),rare,.58f);s.Accent=U.Hex(0xffb3bd);s.Traces=.22f;s.Glow=U.Hex(0xff6475,.28f);s.GlowSpread=30;s.Pins=1;break;
                case "mishima":s.PinA=U.Hex(0xd9dbe3);s.PinB=U.Hex(0x7d808c);s.PinC=U.Hex(0x2c2d34);s.PlateA=U.Hex(0x3b3d48);s.PlateB=U.Hex(0x15151b);s.SubA=U.Hex(0x1c1c22);s.SubB=U.Hex(0x0b0b0f);s.Die=U.Hex(0xff3c5d);s.Edge=U.Hex(0x8f919e);s.Name=U.Hex(0xeceef4);s.Label=U.Hex(0xd6d7df);s.Accent=U.Hex(0xff2448);s.Tracking=.14f;s.Glow=U.Hex(0xff173b,.23f);s.GlowSpread=26;s.Spark=U.Hex(0xff234a);s.SparkGlow=U.Hex(0xf30035);s.Pins=2;break;
                case "ansatsuken":s.PinA=U.Hex(0xffd6da);s.PinB=U.Hex(0xc7475a);s.PinC=U.Hex(0x4a1019);s.PlateA=U.Hex(0x43101d);s.PlateB=U.Hex(0x1a0910);s.SubA=U.Hex(0x200b13);s.SubB=U.Hex(0x10070b);s.Die=U.Hex(0xff4d63);s.Edge=U.Hex(0xff354e);s.Name=U.Hex(0xfff0f1);s.Label=U.Hex(0xff8a97);s.Accent=U.Hex(0xffb2bc);s.Tracking=.12f;s.Glow=U.Hex(0xff214b,.27f);s.GlowSpread=28;s.Spark=U.Hex(0xff6071);s.SparkGlow=U.Hex(0xff294f);s.Pins=3;break;
                case "l44t":s.PinA=U.Hex(0xd4ffe2);s.PinB=U.Hex(0x3fcf74);s.PinC=U.Hex(0x0b4020);s.PlateA=U.Hex(0x103020);s.PlateB=U.Hex(0x06120c);s.SubA=U.Hex(0x06170e);s.SubB=U.Hex(0x030b07);s.Die=U.Hex(0x5eff91);s.Edge=U.Hex(0x3fd979);s.Name=U.Hex(0xdcffe8);s.Label=U.Hex(0xa0ffc0);s.Accent=U.Hex(0x9effbd);s.Tracking=.22f;s.LabelGlow=true;s.Glow=U.Hex(0x36ff78,.26f);s.GlowSpread=30;s.Pins=4;break;
                case "siren":s.PinA=U.Hex(0xd8d1ff);s.PinB=U.Hex(0x3a3150);s.PinC=U.Hex(0x0b0912);s.PlateA=U.Hex(0x1c1527);s.PlateB=U.Hex(0x07060b);s.SubA=U.Hex(0x0b0910);s.SubB=U.Hex(0x050409);s.Die=U.Hex(0xf3efff);s.Edge=U.Hex(0x6d5fb0);s.Name=U.Hex(0xf6f3ff);s.Label=U.Hex(0xc9bfff);s.Accent=U.Hex(0xb7a8ff);s.Tracking=.18f;s.Glow=U.Hex(0x7d5cff,.24f);s.GlowSpread=30;s.Spark=U.Hex(0xd9d0ff);s.SparkGlow=U.Hex(0x9b87ff);s.Void=true;s.Pins=5;break;
            }
            return s;
        }

        // ---- textures (built on first use) ----
        private static Texture2D[] pins;private static Texture2D brush,scan,dieGrid,gloss,diagonal,dots;private const float DiagonalTile=46.669f;
        private static readonly Dictionary<int,Texture2D> cutBands=new Dictionary<int,Texture2D>();
        private static Texture2D Make(int w,int h,bool repeat)=>new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=repeat?TextureWrapMode.Repeat:TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        private static void Fill(Texture2D t,Color[] px){t.SetPixels(px);t.Apply(false,true);}
        private static void Ensure()
        {
            if(brush!=null)return;pins=new Texture2D[6];
            // The spreader's brushed grain: a 1 px white line every 3 px (4 texels per px).
            brush=Make(12,1,true);var px=new Color[12];for(int x=0;x<12;x++)px[x]=new Color(1,1,1,x<4?1:0);Fill(brush,px);
            // Scanlines: the same every 3 px down the card (row 0 is the bottom; the line on the top texels).
            scan=Make(1,12,true);px=new Color[12];for(int y=0;y<12;y++)px[y]=new Color(1,1,1,y>=8?1:0);Fill(scan,px);
            // The die's grid: 1 px lines every 7 px, horizontal at 18% and vertical at 14% (the alpha carries the ratio).
            dieGrid=Make(28,28,true);px=new Color[28*28];for(int y=0;y<28;y++)for(int x=0;x<28;x++){bool h=y<4,v=x>=24;px[y*28+x]=new Color(1,1,1,h?1:v?14f/18f:0);}Fill(dieGrid,px);
            // The die glass's gloss: linear-gradient(135deg, #fff 0, transparent 40%), the alpha along the diagonal.
            gloss=Make(32,32,false);px=new Color[32*32];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float u=(x+(31-y))/62f;px[y*32+x]=new Color(1,1,1,Mathf.Clamp01(1-u/.4f));}Fill(gloss,px);
            // .storm-grid: repeating-linear-gradient(135deg, transparent 0 31px, line 32px 33px): a 1 px diagonal every 33 px.
            // A 46.67 px tile (33 * sqrt 2) holds two whole periods along the diagonal, so it repeats seamlessly.
            diagonal=Make(64,64,true);px=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float g=(x+(63-y)+1)*(DiagonalTile/64)/1.41421f,d=Mathf.Repeat(g,33);px[y*64+x]=new Color(1,1,1,Mathf.Clamp01(1-Mathf.Abs(d-32)/.7f));}Fill(diagonal,px);
            // The body's micro-texture: a 1 px solder-mask dot every 4 px (2 texels per px), every other row offset by half.
            dots=Make(16,16,true);px=new Color[16*16];for(int y=0;y<16;y++)for(int x=0;x<16;x++){int row=y/4;bool on=y%4<2&&(x+(row%2==1?4:0))%8<2;px[y*16+x]=new Color(1,1,1,on?1:0);}Fill(dots,px);
        }
        // ---- the chamfered package ----
        // The chamfer of a card drawn at corner `radius` (ChipSize.Radius), and its silhouette or edge grown `grow` px outward
        // (the keyline, focus and chosen rings: an outline offset by d keeps its cut lines d px out, so the cut grows by
        // (2 - sqrt 2) d along each edge).
        public static float CutOf(float radius)=>radius+3;
        public static void Package(Rect r,float radius,float grow,Color fill)=>Chamfer(new Rect(r.x-grow,r.y-grow,r.width+2*grow,r.height+2*grow),fill,CutOf(radius)+Grow*grow);
        public static void PackageEdge(Rect r,float radius,float grow,Color edge,float width)=>ChamferEdge(new Rect(r.x-grow,r.y-grow,r.width+2*grow,r.height+2*grow),edge,CutOf(radius)+Grow*grow,width);
        private const float Grow=.5858f,Root2=1.41421356f;
        private static void Chamfer(Rect r,Color fill,float cut)=>NativeHudChrome.Cut(r,fill,cut,NativeHudChrome.Corners.Diagonal);
        private static void ChamferEdge(Rect r,Color edge,float cut,float w=1)=>NativeHudChrome.Outline(r,edge,cut,NativeHudChrome.Corners.Diagonal,w);
        // A chamfered vertical gradient: `bottom` under it all, `top` solid over the top cut and fading out by `until` of the height
        // (clear of the bottom cut).
        private static void ChamferFade(Rect r,Color top,Color bottom,float cut,float until)
        {
            Chamfer(r,bottom,cut);float c=Snap(cut),b=2*c+1;if(r.height<2*b)return;NativeHudChrome.Cut(new Rect(r.x,r.y,r.width,b),top,cut,NativeHudChrome.Corners.TL);
            U.Fade(new Rect(r.x,r.y+b,r.width,Mathf.Min(r.height*until,r.height-c)-b),top);
        }
        // A length snapped to whole physical pixels, as NativeHudChrome snaps its cuts.
        private static float Snap(float v)=>Mathf.Round(v*U.S)/Mathf.Max(.01f,U.S);
        // The legs: one metal strip per tier, tiled along the edge. A leg is a 5 px pin of every 15 px, shaded as a round wire
        // (pin-c at its sides, pin-b, a specular line of pin-a), lighter at the tinned tip (pin-a → pin-b → pin-c down its length)
        // and in shadow where it leaves the package.
        private const float LegPitch=15,LegWidth=5;
        private static Texture2D Pins(Style s)
        {
            Ensure();int i=Mathf.Clamp(s.Pins,0,pins.Length-1);if(pins[i]!=null)return pins[i];
            var t=Make(60,40,true);t.wrapModeV=TextureWrapMode.Clamp;var px=new Color[60*40];const int leg=20;
            for(int y=0;y<40;y++)
            {
                float v=1-(y+.5f)/40f;Color along=v<.48f?Color.Lerp(s.PinA,s.PinB,v/.48f):Color.Lerp(s.PinB,s.PinC,(v-.48f)/.52f);float root=v>.74f?1-.5f*(v-.74f)/.26f:1;
                for(int x=0;x<60;x++)
                {
                    if(x>=leg){px[y*60+x]=new Color(0,0,0,0);continue;}float p=(x+.5f)/leg,round=Mathf.Sin(p*Mathf.PI),spec=Mathf.Exp(-(p-.36f)*(p-.36f)/.012f);
                    var c=Color.Lerp(Color.Lerp(s.PinC,along,.35f+.65f*round),s.PinA,.75f*spec);c.r*=root;c.g*=root;c.b*=root;c.a=x==0||x==leg-1?.6f:1;px[y*60+x]=c;
                }
            }
            Fill(t,px);return pins[i]=t;
        }
        // The legs on the span between the insets: as many as fit, centred on it.
        private static int LegCount(float span)=>Mathf.Max(0,Mathf.FloorToInt((span-LegWidth)/LegPitch)+1);
        private static float LegStart(float span)=>(span-((LegCount(span)-1)*LegPitch+LegWidth))/2;
        public static void Legs(Rect r,Style s,float inset,float height,float a)
        {
            var t=Pins(s);float span=r.width-2*inset,o=LegStart(span);var top=new Rect(r.x+inset,r.y-height,span,height);var bottom=new Rect(r.x+inset,r.yMax,span,height);
            U.Tile(top,t,new Color(1,1,1,a),new Vector2(LegPitch,height),new Vector2(-o,0));U.Tile(bottom,t,new Color(1,1,1,a),new Vector2(LegPitch,height),new Vector2(-o,0),true);
            // The package's lip over the legs' roots.
            U.Fill(new Rect(top.x,r.y-1,span,1),new Color(0,0,0,.45f*a));U.Fill(new Rect(bottom.x,r.yMax,span,1),new Color(0,0,0,.45f*a));
        }
        // The body: sub-a over the top fading into sub-b (Siren: the void-glass radial), the solder-mask dots below the spreader
        // (`below`), the tier's light falling from the spreader onto it, and the inner shadow along its sides and bottom.
        public static void Body(Rect r,Style s,Color rare,float radius,float below,float a,bool plain)
        {
            float cut=CutOf(radius);Ensure();
            float C=Snap(cut);
            if(s.Void)
            {   // radial-gradient(130% 75% at 50% 112%, #3a2166, #160d27 42%, #07060b 72%) over #050409, inside the chamfer (clipped,
                // so not under a rotation: Plain).
                Chamfer(r,U.A(s.SubB,a),cut);var e=new Rect(r.center.x-1.3f*r.width,r.yMax+.12f*r.height-.75f*r.height,2.6f*r.width,1.5f*r.height);
                if(!plain)for(int b=1;b<3;b++){U.BeginBand(Band(Inset(r,1),C,b));U.RadialStops(e,U.Hex(0x07060b,a),Stops3,Alpha3);U.RadialStops(e,U.Hex(0x160d27,a),Stops3,Alpha2);U.RadialStops(e,U.Hex(0x3a2166,a),Stops2,Alpha1);U.EndBand();}
            }
            else ChamferFade(r,U.A(s.SubA,a),U.A(s.SubB,a),cut,.85f);
            Color dot=new Color(1,1,1,(s.Void?.022f:.032f)*a);var field=Rect.MinMaxRect(r.x+2,below,r.xMax-2,r.yMax-C);if(field.height>0)U.Tile(field,dots,dot,new Vector2(4,4),new Vector2(0,below-r.y));
            if(below<r.yMax-C)U.Tile(Rect.MinMaxRect(r.x+2,r.yMax-C,r.xMax-C,r.yMax-2),dots,dot,new Vector2(4,4),new Vector2(0,r.yMax-C-r.y));
            if(!s.Void)U.Fade(new Rect(r.x+1,below-2,r.width-2,Mathf.Min(46,r.yMax-cut-below)),U.A(rare,.07f*a));
            // The inner shadow: the body darkening into its sides and bottom, so the face sits inside the frame.
            Color shade=new Color(0,0,0,.32f*a);float d=Mathf.Min(14,r.width*.06f);U.HGradient(new Rect(r.x+1,r.y+C,d,r.height-2*C),shade,Color.clear);U.HGradient(new Rect(r.xMax-1-d,r.y+C,d,r.height-2*C),Color.clear,shade);
            U.Fade(new Rect(r.x+C,r.yMax-1-d,r.width-2*C,d),shade,false);
        }
        private static readonly float[] Stops3={0,.42f,.72f,1},Alpha3={0,0,1,1},Alpha2={1,1,0,0},Stops2={0,.42f,1},Alpha1={1,0,0};
        private static Rect Inset(Rect r,float d)=>new Rect(r.x+d,r.y+d,r.width-2*d,r.height-2*d);
        // The frame over the body and the kits' art: the crisp edge (`edge`: the tier's, brighter under the pointer) with a dark
        // keyline outside it, the bevel (a light line inside the top edge from the cut, a dark one inside the bottom), the tier's
        // accent runs (along the top from the top-left cut, along the bottom into the bottom-right one) and the corner marks on
        // the two square corners, as the HUD panels carry them.
        public static void Frame(Rect r,Style s,Color edge,float radius,float h,float a)
        {
            float cut=Snap(CutOf(radius)),p=NativeHudChrome.Px,run=Mathf.Min(r.width*.36f,96);Color accent=Color.Lerp(s.Accent,Color.white,.25f*h);
            ChamferEdge(r,U.A(edge,a),cut);
            U.Fill(new Rect(r.x+cut,r.y+p,r.width-cut-p,p),U.A(Color.Lerp(U.Hex(0xffffff,.1f),s.Accent,.3f),a));U.Fill(new Rect(r.x+p,r.yMax-2*p,r.width-cut-p,p),new Color(0,0,0,.5f*a));
            U.Fill(new Rect(r.x+cut,r.y,run,2*p),U.A(accent,(.85f+.15f*h)*a));U.Fill(new Rect(r.xMax-cut-run*.5f,r.yMax-2*p,run*.5f,2*p),U.A(accent,.6f*a));
            float arm=Mathf.Min(9,r.height*.12f);Color mark=U.A(accent,.7f*a);
            U.Fill(new Rect(r.xMax-arm,r.y,arm,2*p),mark);U.Fill(new Rect(r.xMax-2*p,r.y,2*p,arm),mark);U.Fill(new Rect(r.x,r.yMax-2*p,arm,2*p),mark);U.Fill(new Rect(r.x,r.yMax-arm,2*p,arm),mark);
        }
        // The circuit traces anchored bottom-right (300 x 150 px) in the rarity colour at the tier's opacity, over a pool of
        // its light.
        private const string TraceMarkup="<path d=\"M300 184H236l-18 18v98M300 200h-52l-14 14v86M300 216h-40l-10 10v74M300 262h-22l-10 10v28M196 300v-20l-12-12h-38M168 300v-12l-8-8h-30M300 238h-34\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/>"
            +"<circle cx=\"143\" cy=\"268\" r=\"3.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/><circle cx=\"127\" cy=\"280\" r=\"3.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/><circle cx=\"263\" cy=\"238\" r=\"3.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.6\"/>";
        public static void Traces(Rect r,Color rare,Style s,float a)
        {
            // The circuit's glow under them: the tier's light pooled in the lower right, its ellipse inside the package.
            if(!s.Void)U.RadialStops(Rect.MinMaxRect(r.x+r.width*.28f,r.y+r.height*.3f,r.xMax-1,r.yMax-1),U.A(rare,.075f*a),PoolAt,PoolAlpha);
            NativeIcons.DrawMarkup(new Rect(r.xMax-299,r.yMax-299,300,300),"chip-traces",TraceMarkup,300,U.A(rare,s.Traces*a));
        }
        private static readonly float[] PoolAt={0,.45f,1},PoolAlpha={1,.45f,0};
        // The heat spreader: a chamfered plate on its drop shadow, anodised (plate-a over plate-b) and lit from the die's side by
        // the tier, Mythic's drifting foil, the brushed grain, a glass sheen, its bevel (a light top line from the cut in rarity 45% into
        // #ffffff1f, a dark bottom line and a faint inner ring) and the tier's light strip under it. The light sweep is the
        // caller's (over the die).
        public const float PlateCut=6;
        public static void Plate(Rect plate,Style s,Color rare,bool foil,float t,float a)
        {
            float c=Snap(PlateCut),p=NativeHudChrome.Px,lit=plate.width*.78f;
            Chamfer(new Rect(plate.x,plate.y+3,plate.width,plate.height),new Color(0,0,0,(s.Void?1:.55f)*a),PlateCut);ChamferFade(plate,U.A(s.PlateA,a),U.A(s.PlateB,a),PlateCut,.78f);
            if(!s.Void)
            {   // The tier's light from the die's end, kept off the cut corners.
                Color tint=U.A(rare,.16f*a);U.HGradient(new Rect(plate.x+c,plate.y+p,lit-c,c),tint,Color.clear);U.HGradient(new Rect(plate.x+p,plate.y+c,lit,plate.height-2*c),tint,Color.clear);U.HGradient(new Rect(plate.x+p,plate.yMax-c,lit,c-p),tint,Color.clear);
            }
            if(foil)Holographic(plate,t,a);Brushed(plate,a);
            // The glass sheen over its upper half, from the cut.
            U.Fade(new Rect(plate.x+c,plate.y+p,plate.width-c-p,plate.height*.45f),new Color(1,1,1,(s.Void?.025f:.045f)*a));
            U.Fill(new Rect(plate.x+c,plate.y,plate.width-c-p,p),s.Void?U.Hex(0xb7a8ff,.18f*a):U.A(U.Mix(U.Hex(0xffffff,.14f),rare,.45f),a));U.Fill(new Rect(plate.x+p,plate.yMax-p,plate.width-c-p,p),new Color(0,0,0,(s.Void?1:.66f)*a));
            ChamferEdge(plate,s.Void?U.Hex(0xb7a8ff,.11f*a):new Color(1,1,1,.05f*a),PlateCut);
            // The light strip under the spreader: the tier's light along its lower edge, brightest under the middle.
            Color led=s.Void?U.Hex(0x9b87ff):s.Accent;float ly=plate.yMax+p,half=(plate.width-2*c)/2;U.Glow(new Rect(plate.x+c+half*.4f,ly,half*1.2f,p),U.A(led,.14f*a),4);
            U.HGradient(new Rect(plate.x+c,ly,half,p),Color.clear,U.A(led,.55f*a));U.HGradient(new Rect(plate.x+c+half,ly,half,p),U.A(led,.55f*a),Color.clear);
        }
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
        // The die in its socket, under the glow of its light (`glow` its colour and strength, `spread` px): a chamfered bezel
        // (a light ring, the dark ground) with the tier's corner marks on its square corners, the die's rim in its colour, the
        // radial light, the inner shadow, the grid, the glass's gloss and its rim light. The icon is the caller's.
        public const float DieCut=5;
        public static void DieWindow(Rect die,Style s,Color dieC,Color glow,float spread,float h,float bright,float a)
        {
            Color ground=U.Hex(s.Void?0x050409:0x0a0b0e),rim=s.Void?U.Hex(0xb7a8ff,.6f):U.A(dieC,.55f);float p=NativeHudChrome.Px;
            U.Glow(die,U.A(glow,a),spread);
            Chamfer(new Rect(die.x-4,die.y-4,die.width+8,die.height+8),s.Void?U.Hex(0xb7a8ff,.15f*a):new Color(1,1,1,(.09f+.05f*h)*a),DieCut+2.4f);Chamfer(new Rect(die.x-3,die.y-3,die.width+6,die.height+6),U.A(ground,a),DieCut+1.8f);
            float arm=Mathf.Max(3,die.width*.12f);Color mark=U.A(Color.Lerp(dieC,Color.white,.2f),(.55f+.3f*h)*a);var sk=new Rect(die.x-4,die.y-4,die.width+8,die.height+8);
            U.Fill(new Rect(sk.xMax-arm,sk.y,arm,p),mark);U.Fill(new Rect(sk.xMax-p,sk.y,p,arm),mark);U.Fill(new Rect(sk.x,sk.yMax-p,arm,p),mark);U.Fill(new Rect(sk.x,sk.yMax-arm,p,arm),mark);
            Chamfer(die,U.A(ground,a),DieCut);
            float glowR=die.width*1.47f;var light=new Rect(die.center.x-glowR/2,die.y+die.height*.46f-glowR/2,glowR,glowR);
            if(s.Void)U.RadialStops(light,U.Hex(0x2d2244,a),DieStopsSiren,DieAlpha);else U.RadialStops(light,U.A(dieC,Mathf.Min(1,.26f*bright)*a),DieStops,DieAlpha);
            U.RadialStops(die,new Color(0,0,0,a),InnerAt,InnerAlpha);DieGlass(die,dieC,a);ChamferEdge(die,U.A(rim,a),DieCut);
        }
        private static readonly float[] DieStops={0,.72f,1},DieStopsSiren={0,.7f,1},DieAlpha={1,0,0},InnerAt={0,.55f,1},InnerAlpha={0,0,.7f};
        // The die's glass: the grid (die 18% / 14%, .6 opacity, inset 6 px), the 135deg gloss (#ffffff30 to clear at 40%) from
        // the cut, and the rim light along the top and down the left.
        public static void DieGlass(Rect die,Color tint,float a)
        {
            Ensure();float inset=Mathf.Min(6,die.width*.12f),c=Snap(DieCut),p=NativeHudChrome.Px;U.Tile(new Rect(die.x+inset,die.y+inset,die.width-2*inset,die.height-2*inset),dieGrid,U.A(tint,.108f*a),new Vector2(7,7));
            U.Image(new Rect(die.x+c*.5f,die.y+c*.5f,die.width-c*.5f,die.height-c*.5f),gloss,new Color(1,1,1,.19f*a));
            U.HGradient(new Rect(die.x+c,die.y+p,die.width-c-2*p,p),new Color(1,1,1,.32f*a),new Color(1,1,1,.04f*a));U.Fade(new Rect(die.x+p,die.y+c,p,die.height*.55f),new Color(1,1,1,.2f*a));
        }

        // ---- the exclusive kits' auras (z-index 1: over the body and particles, under the frame and the package's content) ----
        // Art that reaches a cut corner is drawn once per face band, so it stays inside the chamfer: band 0 the top strip right
        // of the top-left cut, 1 the full width between the cuts, 2 the bottom strip left of the bottom-right cut.
        private static Rect Band(Rect r,float C,int i)=>i==0?Rect.MinMaxRect(r.x+C,r.y,r.xMax,r.y+C):i==1?Rect.MinMaxRect(r.x,r.y+C,r.xMax,r.yMax-C):Rect.MinMaxRect(r.x,r.yMax-C,r.xMax-C,r.yMax);
        public static void Aura(Rect r,float radius,string tier,float t,float a,bool plain)
        {
            if(plain)return;float C=Snap(CutOf(radius));
            switch(tier){case "mishima":Emperor(r,C,t,a);break;case "ansatsuken":Fighting(r,C,t,a);break;case "l44t":Rain(r,C,t,a);break;case "siren":Siren(r,C,t,a);break;}
        }
        // emperor-card.ts: a red current orbiting the package edge (conic, 3.2 s), the storm bolt, three current arcs and a grid.
        private static readonly Vector2[] Arc={new Vector2(0,.12f),new Vector2(.24f,.36f),new Vector2(.43f,.21f),new Vector2(.65f,.53f),new Vector2(.76f,.45f),new Vector2(1,.74f),new Vector2(.76f,.49f),new Vector2(.65f,.59f),new Vector2(.43f,.26f),new Vector2(.24f,.41f)};
        private static readonly Vector2[] ArcFlip={new Vector2(1,.88f),new Vector2(.76f,.64f),new Vector2(.57f,.79f),new Vector2(.35f,.47f),new Vector2(.24f,.55f),new Vector2(0,.26f),new Vector2(.24f,.51f),new Vector2(.35f,.41f),new Vector2(.57f,.74f),new Vector2(.76f,.59f)};
        private static readonly float[] Flat={0,1},Full={1,1};
        private static string bolt,seal,face,flames;
        private static void Emperor(Rect r,float C,float t,float a)
        {
            BuildMarkup();Ensure();var face=Inset(r,1);
            for(int b=0;b<3;b++)
            {
                U.BeginBand(Band(face,C,b));U.Tile(face,diagonal,U.Hex(0x90929e,.063f*a),new Vector2(DiagonalTile,DiagonalTile));
                if(b>0)
                {
                    NativeIcons.DrawMarkup(new Rect(r.xMax+16-150,r.yMax+22-165,150,150),"chip-storm-bolt",bolt,100,U.Hex(0xff1742,.09f*a));
                    // .storm-arc: stormCurrent 2.6 s ease-in-out (.18 → .75 at 45-65% → .18), delays 0 / -1 / -1.8 s, with its drop-shadow.
                    for(int i=0;i<3;i++)
                    {
                        float k=Mathf.Repeat(t+(i==0?0:i==1?1:1.8f),2.6f)/2.6f,e=k<.45f?Smooth(k/.45f):k<.65f?1:1-Smooth((k-.65f)/.35f),op=Mathf.Lerp(.18f,.75f,e)*a;
                        float top=i==0?.8f:i==1?.88f:.84f,h=r.height*.22f*(i==2?.7f:1);var box=new Rect(r.x-r.width*.05f,r.y+r.height*top+(i==2?r.height*.22f*.15f:0),r.width*1.1f,h);
                        var shape=i==1?ArcFlip:Arc;Color c=i==0?U.Hex(0xef2149):i==1?U.Hex(0xbd1235):U.Hex(0x080408);
                        U.Polygon(new Rect(box.x,box.y-1.5f,box.width,box.height+3),shape,Flat,Full,U.Hex(0xfb254b,.35f*op));U.Polygon(box,shape,Flat,Full,U.A(c,op));
                    }
                }
                U.EndBand();
            }
            // .emperor-aura:before: conic-gradient(from storm-angle, clear 0 60%, #ff244870 72%, #fff 77%, #ff2448 80%, clear 90%)
            // in a 2 px ring inside the border, the angle turning once every 3.2 s; each run stops short of the corners (the cut
            // ones by the cut).
            float turn=360*Mathf.Repeat(t/3.2f,1);var ring=Inset(r,1);
            for(int side=0;side<4;side++)
            {
                bool horizontal=side%2==0;float from=side%2==0?C+2:6,to=side%2==0?6:C+2,len=(horizontal?ring.width:ring.height)-from-to;int n=Mathf.Max(1,Mathf.CeilToInt(len/5));float step=len/n;
                for(int i=0;i<n;i++)
                {
                    float along=from+(i+.5f)*step;Vector2 p=side==0?new Vector2(ring.x+along,ring.y+1):side==1?new Vector2(ring.xMax-1,ring.y+along):side==2?new Vector2(ring.xMax-along,ring.yMax-1):new Vector2(ring.x+1,ring.yMax-along);
                    float deg=Mathf.Atan2(p.x-ring.center.x,ring.center.y-p.y)*Mathf.Rad2Deg,u=Mathf.Repeat((deg-turn)/360,1);if(u<.6f||u>.9f)continue;
                    Color c=u<.72f?U.Hex(0xff2448,.44f*(u-.6f)/.12f):u<.77f?Color.Lerp(U.Hex(0xff2448,.44f),Color.white,(u-.72f)/.05f):u<.8f?Color.Lerp(Color.white,U.Hex(0xff2448),(u-.77f)/.03f):U.Hex(0xff2448,1-(u-.8f)/.1f);
                    U.Fill(horizontal?new Rect(p.x-step/2,p.y-1,step+.4f,2):new Rect(p.x-1,p.y-step/2,2,step+.4f),U.A(c,a));
                }
            }
        }
        // ansatsuken-card.ts: the ink seal, the red headband tied across the top, two ki rings orbiting and the dojo grid.
        private static readonly Vector2[] Band4={new Vector2(0,.38f),new Vector2(1,0),new Vector2(1,.62f),new Vector2(0,1)};
        private static readonly float[] BandAtA={0,.35f,.5f,1},BandA={0,1,.2f,0},BandAtB={0,.35f,.5f,.8f,1},BandB={0,0,1,0,0},BandAtC={0,.5f,.8f,1},BandC={0,.3f,1,0};
        private static void Fighting(Rect r,float C,float t,float a)
        {
            BuildMarkup();float gh=r.height*.36f,cy=r.yMax-gh/2,cos=Mathf.Cos(25*Mathf.Deg2Rad),sin=Mathf.Sin(25*Mathf.Deg2Rad);
            float k2=Mathf.PingPong(t/1.4f,1),flow=Smooth(k2),tilt=Mathf.Tan(2.5f*Mathf.Deg2Rad)*r.width*1.2f;var band=new Rect(r.x-r.width*.1f+8*flow,r.y+3-tilt/2,r.width*1.2f,8+tilt);float op=(.8f+.2f*flow)*a;
            for(int b=0;b<3;b++)
            {
                U.BeginBand(Band(r,C,b));
                if(b>0)
                {
                    NativeIcons.DrawMarkup(new Rect(r.xMax+26-170,r.yMax+30-170,170,170),"chip-warrior-seal",seal,100,U.Hex(0xff294b,.1f*a));
                    // .dojo-grid: the bottom 36%, a line every 16 px (#ff354e14) under perspective(90px) rotateX(25deg).
                    for(float d=15.5f;d<gh;d+=16){float y=gh/2-d,z=y*sin,k=90/(90-z);float py=cy+y*cos*k,w=r.width*k;U.Fill(new Rect(r.center.x-w/2,py-.5f*k,w,Mathf.Max(.6f,k)),U.Hex(0xff354e,.08f*a));}
                }
                // .red-headband: transparent → #ff344f 35% → #ff999d 50% → #b41634 80% → transparent, 120% wide, tilted, flowing (2.8 s).
                if(b<2){U.Polygon(band,Band4,BandAtA,BandA,U.Hex(0xff344f,op));U.Polygon(band,Band4,BandAtC,BandC,U.Hex(0xb41634,op));U.Polygon(band,Band4,BandAtB,BandB,U.Hex(0xff999d,op));}
                U.EndBand();
            }
            // .ki-ring x2: 2 px #ff35554d circles (220 px and 180 px) with their left and bottom borders clear, turning (4 s, 3 s back).
            var centre=new Vector2(r.xMax-40,r.yMax-30);U.BeginBand(r);
            for(int ring=0;ring<2;ring++)
            {
                float radius=ring==0?110:90,rot=360*Mathf.Repeat(t/(ring==0?4:3f),1)*(ring==0?1:-1);int n=Mathf.CeilToInt(Mathf.PI*radius/2.6f);
                for(int i=0;i<=n;i++)
                {
                    float ang=(-135+180f*i/n+rot)*Mathf.Deg2Rad;var p=centre+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*(radius-1);if(p.x<r.x||p.x>r.xMax||p.y<r.y||p.y>r.yMax)continue;
                    float edge=Mathf.Clamp01(Mathf.Min(i,n-i)/(n*.06f));U.Fill(new Rect(p.x-1.1f,p.y-1.1f,2.2f,2.2f),U.Hex(0xff3555,.3f*edge*a));
                }
            }
            U.EndBand();
        }
        // matrixRain(8): eight columns of 1s and 0s (15 px mono, line 21 px, #5eff91 at .3) falling over 4 s, faded at both ends;
        // a digit over a cut corner is drawn in the face bands.
        private static readonly string[] Bit={"0","1"};
        private static void Rain(Rect r,float C,float t,float a)
        {
            const float line=21,column=32*line;U.BeginBand(r);
            for(int i=0;i<8;i++)
            {
                float phase=Mathf.Repeat((t+(i%7)*.37f)/4,1),top=r.y-r.height+2.1f*column*phase,x=r.x+r.width*i*.125f;
                int first=Mathf.Max(0,Mathf.FloorToInt((r.y-top)/line)),last=Mathf.Min(31,Mathf.CeilToInt((r.yMax-top)/line));
                for(int n=first;n<=last;n++)
                {
                    float y=top+n*line,f=(y+line/2-r.y)/r.height,mask=f<.2f?f/.2f:f>.8f?(1-f)/.2f:1;if(mask<=.02f)continue;
                    var q=new Rect(x,y,12,line);string bit=Bit[(i*7+n*13)%5<2?1:0];Color c=U.Hex(0x5eff91,.3f*Mathf.Clamp01(mask)*a);
                    if(q.x<r.x+C&&q.y<r.y+C||q.xMax>r.xMax-C&&q.yMax>r.yMax-C){U.EndBand();for(int b=0;b<3;b++){U.BeginBand(Band(r,C,b));U.Text(q,bit,15,c,TextAnchor.MiddleLeft,mono:true);U.EndBand();}U.BeginBand(r);}
                    else U.Text(q,bit,15,c,TextAnchor.MiddleLeft,mono:true);
                }
            }
            U.EndBand();
        }
        // siren-card.ts sirenAura: black flame tongues licking up the bottom, the grin watching from the fire, scanlines, and on
        // the glitch beat (65-72% of 4.2 s) the grin flickers and a tear crosses the package.
        private static void Siren(Rect r,float C,float t,float a)
        {
            BuildMarkup();Ensure();float g=Mathf.Repeat(t,4.2f)/4.2f;var inner=Inset(r,1);
            // .siren-flames: left -4%, width 108%, the bottom 42%, its 200 x 70 view box sliced to the box's width (xMidYMax).
            // The tongues' siren-lick, as one slow breath of the row's height (no transform inside the clip). It only ever shrinks the
            // drawn height, so the raster size (the width) and its cached texture stay the same every frame.
            var box=new Rect(r.x-r.width*.04f,r.yMax-r.height*.42f+2,r.width*1.08f,r.height*.42f);float s=Mathf.Max(box.width/200,box.height/70),lick=.95f+.05f*Mathf.Sin(t*2*Mathf.PI/1.7f);
            // .siren-face: 70% wide at right -6%, bottom 2%, turned -6deg; siren-watch flickers .16 → .42 / .03 / .32 on the beat.
            float watch=g>=.65f&&g<.66f?.42f:g>=.66f&&g<.68f?.03f:g>=.68f&&g<.72f?.32f:.16f,fw=r.width*.7f;
            for(int b=0;b<3;b++)
            {
                var clip=Band(inner,C,b);U.BeginBand(clip);
                if(b>0)
                {
                    U.BeginBand(box);NativeIcons.DrawMarkup(new Rect(box.center.x-100*s,box.yMax-200*s*lick,200*s,200*s*lick),"chip-siren-flames",flames,200,new Color(1,1,1,a),320);U.EndBand();
                    NativeIcons.DrawMarkup(new Rect(r.xMax+r.width*.06f-fw,r.yMax-r.height*.02f-fw*.6f,fw,fw),"chip-siren-face",face,200,new Color(1,1,1,watch*a),320);
                }
                U.Tile(r,scan,new Color(1,1,1,.031f*a),new Vector2(1,3));
                // .siren-tear: a 12 px band (transparent → #b7a8ff40 20% → #f6f3ff70 50% → #2de2ff38 72% → transparent) on the beat.
                if(b==1&&g>=.65f&&g<.72f)
                {
                    int step=g<.67f?0:g<.69f?1:2;float y=r.y+r.height*(step==0?.22f:step==1?.61f:.44f),dx=step==0?-8:step==1?10:-5,op=(step==1?.8f:1)*a,x0=r.x-r.width*.02f+dx,w=r.width*1.04f;
                    U.HGradient(new Rect(x0,y,w*.2f,12),Color.clear,U.Hex(0xb7a8ff,.25f*op));U.HGradient(new Rect(x0+w*.2f,y,w*.3f,12),U.Hex(0xb7a8ff,.25f*op),U.Hex(0xf6f3ff,.44f*op));
                    U.HGradient(new Rect(x0+w*.5f,y,w*.22f,12),U.Hex(0xf6f3ff,.44f*op),U.Hex(0x2de2ff,.22f*op));U.HGradient(new Rect(x0+w*.72f,y,w*.28f,12),U.Hex(0x2de2ff,.22f*op),Color.clear);
                }
                U.EndBand();
            }
        }
        // siren-jolt (4.2 s, steps): the header's shake on the glitch beat, px.
        public static float Jolt(float t){float g=Mathf.Repeat(t,4.2f)/4.2f;return g<.65f||g>=.72f?0:g<.66f?-4:g<.67f?5:g<.69f?-2:g<.70f?3:-1;}
        private static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}

        // ---- the holo scan: the draft tray's highlighted card (NativeHoloScan's timeline; NativeChipCard, NativeDraftScreen) ----
        // A tier-lit edge traced round the package's chamfered outline both ways from the top-left cut, then one comet a lap;
        // the legs powering up in the tier's pin colour as the trace passes them (brighter as the comet runs by); corner brackets
        // that snap in outside the card over a dark keyline (so they read over the pilot's light torso); one scan line down the
        // face under the text, over faint 3 px holo lines; and a halo of the tier's light behind the package (the die's charge is
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
        // The package's outline as NativeHudChrome draws it: the cut snapped to whole physical pixels and the 1.5 px line, so the
        // traced stretches and the comet meet the finished ring exactly.
        private static void EdgeOf(float radius,out float C,out float w){C=Snap(CutOf(radius));w=NativeHudChrome.Snap(1.5f);}
        private static Color Cycle(float u,Color[] c){float x=Mathf.Repeat(u,1)*c.Length;int i=(int)x%c.Length;return Color.Lerp(c[i],c[(i+1)%c.Length],x-Mathf.Floor(x));}
        // Behind the package: the halo of the tier's light (breathing, NativeHoloScan.Swell) and a tight glow of its edge.
        public static void HoloHalo(Rect r,string tier,Color rare,float k,float t,bool calm)
        {
            var h=HoloOf(tier,rare);U.Glow(r,U.A(h.Halo,h.HaloAlpha*k*NativeHoloScan.Swell(t,calm)),40);U.Glow(r,U.A(h.Edge,.16f*k),10);
        }
        // On the package's border: the trace, then the finished ring (Mythic's drifting foil) and the comet with its bead.
        public static void HoloEdge(Rect r,float radius,string tier,Color rare,float k,float age,float t,bool calm)
        {
            var h=HoloOf(tier,rare);EdgeOf(radius,out float C,out float w);var q=r;float L=Perimeter(q,C),start=L-C*Root2/2,ring=.34f*k;
            // Siren: the edge jolts with the spreader on the glitch beat, a cyan copy on the other side.
            float jolt=h.Glitch&&!calm?Jolt(t)*.75f:0;if(jolt!=0){ChamferEdge(new Rect(q.x-jolt,q.y,q.width,q.height),U.A(GlitchCyan,.35f*k),C,1.5f);q.x+=jolt;}
            Color edge=h.Foil&&!calm?Cycle(t/7,FoilTail):h.Edge;float reach=NativeHoloScan.TraceReach(age,calm);
            if(reach>=1)ChamferEdge(q,U.A(edge,ring),C,1.5f);
            else
            {   // The trace: both ways from the middle of the top-left cut, its leading tenth ramping up to the head, a bead on each end.
                float half=reach*L/2,lead=Mathf.Min(half,L*.1f);Stretch(q,C,w,start-half,start+half,ring,ring,edge);
                Stretch(q,C,w,start+half-lead,start+half,0,.9f*k,h.Head);Stretch(q,C,w,start-half,start-half+lead,.9f*k,0,h.Head);Bead(Along(q,C,w,start+half),h.Head,k);Bead(Along(q,C,w,start-half),h.Head,k);
            }
            // The comet: from where the trace closed, clockwise once a lap, its tail (twice the line) growing in; Legendary's gold
            // and Mythic's foil in bands.
            if(!NativeHoloScan.Comet(age,calm,out float head,out float tail))return;float hd=start+head*L,len=tail*L+.01f;
            if(h.Tail==null)Stretch(q,C,2*w,hd-len,hd,0,k,h.Head);
            else{int n=h.Tail.Length;for(int s=0;s<n;s++)Stretch(q,C,2*w,hd-len*(s+1f)/n,hd-len*s/(float)n,k*(1-(s+1f)/n),k*(1-s/(float)n),h.Tail[s]);}
            Bead(Along(q,C,w,hd),h.Head,k);
        }
        // A round white-hot bead in a glow of the head colour.
        private static void Bead(Vector2 p,Color head,float k){U.Glow(new Rect(p.x-2,p.y-2,4,4),U.A(head,.7f*k),8);U.Glow(new Rect(p.x-.8f,p.y-.8f,1.6f,1.6f),U.A(Color.Lerp(head,Color.white,.6f),Mathf.Min(1,k)),2.2f);}
        // The outline clockwise from the top run's left end (where the top-left cut meets it): the top, the right side, the
        // bottom-right cut, the bottom, the left side, the top-left cut.
        private static float Perimeter(Rect q,float C)=>2*(q.width-C)+2*(q.height-C)+2*C*Root2;
        private static Vector2 Along(Rect q,float C,float w,float d)
        {
            float a=q.width-C,b=q.height-C,g=C*Root2,o=w/2,n=o/Root2;d=Mathf.Repeat(d,Perimeter(q,C));
            if(d<a)return new Vector2(q.x+C+d,q.y+o);d-=a;if(d<b)return new Vector2(q.xMax-o,q.y+d);d-=b;
            if(d<g){float f=d/g;return new Vector2(q.xMax-C*f-n,q.yMax-C+C*f-n);}d-=g;
            if(d<a)return new Vector2(q.xMax-C-d,q.yMax-o);d-=a;if(d<b)return new Vector2(q.x+o,q.yMax-d);d-=b;
            float e=Mathf.Clamp01(d/g);return new Vector2(q.x+C*e+n,q.y+C-C*e+n);
        }
        // The outline from d0 to d1 (any range, wrapping) at alpha a0 → a1 along it, `w` thick inside the border.
        private static void Stretch(Rect q,float C,float w,float d0,float d1,float a0,float a1,Color c)
        {
            float L=Perimeter(q,C);if(d1-d0<=.01f||L<=0)return;if(d1-d0>L)d0=d1-L;float shift=Mathf.Floor(d0/L)*L;d0-=shift;d1-=shift;
            if(d1<=L){Runs(q,C,w,d0,d1,a0,a1,c);return;}float am=Mathf.Lerp(a0,a1,(L-d0)/(d1-d0));Runs(q,C,w,d0,L,a0,am,c);Runs(q,C,w,0,d1-L,am,a1,c);
        }
        private static void Runs(Rect q,float C,float w,float d0,float d1,float a0,float a1,Color c)
        {
            float a=q.width-C,b=q.height-C,g=C*Root2,at=0;
            for(int p=0;p<6;p++)
            {
                float len=p==2||p==5?g:p==0||p==3?a:b,s0=Mathf.Max(d0,at),s1=Mathf.Min(d1,at+len);
                if(s1>s0&&len>0)
                {
                    float b0=Mathf.Lerp(a0,a1,(s0-d0)/(d1-d0)),b1=Mathf.Lerp(a0,a1,(s1-d0)/(d1-d0)),u0=s0-at,u1=s1-at;
                    switch(p)
                    {
                        case 0:Ramp(new Rect(q.x+C+u0,q.y,u1-u0,w),true,b0,b1,c);break;
                        case 1:Ramp(new Rect(q.xMax-w,q.y+u0,w,u1-u0),false,b0,b1,c);break;
                        case 3:Ramp(new Rect(q.xMax-C-u1,q.yMax-w,u1-u0,w),true,b1,b0,c);break;
                        case 4:Ramp(new Rect(q.x,q.yMax-u1,w,u1-u0),false,b1,b0,c);break;
                        default:Cut(q,C,w,p==5,(u1-u0)/len*(b0+b1)/2,c);break;
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
        // A cut's diagonal (`topLeft`, else the bottom-right one): its line baked at the physical size, drawn whole at the
        // stretch's mean alpha over the share of it lit (the trace crosses a cut within a frame, the comet in a few).
        private static void Cut(Rect q,float C,float w,bool topLeft,float al,Color c)
        {
            if(al<=.002f)return;var box=topLeft?new Rect(q.x,q.y,C,C):new Rect(q.xMax-C,q.yMax-C,C,C);U.Image(box,CutBand(topLeft?0:1,Mathf.RoundToInt(C*U.S),w*U.S*1.15f),U.A(c,Mathf.Min(1,al)));
        }
        // A cut corner's line `px` physical pixels square, `band` px thick inside the diagonal, 4x4 supersampled as NativeHudChrome
        // bakes its outline corners (corner 0 the top-left cut, 1 the bottom-right); cached per size.
        private static Texture2D CutBand(int corner,int px,float band)
        {
            px=Mathf.Clamp(px,1,96);int key=corner*1000000+px*1000+Mathf.RoundToInt(band*8);if(cutBands.TryGetValue(key,out var t)&&t!=null)return t;var c=new Color[px*px];
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                int hit=0;for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++){float fx=x+(sx+.5f)/4,fy=y+(sy+.5f)/4,ux=corner==0?fx:px-fx,uy=corner==0?fy:px-fy,d=(ux+uy-px)/Root2;if(d>=0&&d<band)hit++;}
                c[(px-1-y)*px+x]=new Color(1,1,1,hit/16f);
            }
            t=Make(px,px,false);Fill(t,c);cutBands[key]=t;return t;
        }
        // How lit a leg is 0..1 as the trace runs both ways from the middle of the top-left cut, `reach` of each arm (half the
        // outline) gone: the top row on the clockwise arm, the bottom row on the anticlockwise one (down the left side). `legX`
        // the leg's centre from the card's left edge; fully lit NativeHoloScan.Ramp px behind the trace.
        private static float LegLit(float reach,Rect r,float C,float legX,bool bottom)
        {
            float d=C*Root2/2+(bottom?r.height-C+legX:legX-C);return Mathf.Clamp01((Mathf.Clamp01(reach)*Perimeter(r,C)/2-d)/NativeHoloScan.Ramp);
        }
        // The legs power up in the tier's pin colour as the trace passes them (calm: all lit, still), each row over a soft glow
        // as long as its lit legs, a leg brighter as the comet runs by; the tips stay bare metal.
        public static void HoloLegs(Rect r,float radius,float inset,float pin,string tier,Color rare,float k,float age,bool calm)
        {
            var h=HoloOf(tier,rare);EdgeOf(radius,out float C,out float w);float span=r.width-2*inset,reach=NativeHoloScan.TraceReach(age,calm),L=Perimeter(r,C),start=LegStart(span);int n=LegCount(span);
            bool comet=NativeHoloScan.Comet(age,calm,out float head,out _);float hc=Mathf.Repeat(L-C*Root2/2+head*L,L);Color hot=Color.Lerp(h.Pin,Color.white,.35f);
            for(int row=0;row<2;row++)
            {
                bool bottom=row==1,any=false;float sum=0,x0=0,x1=0,y=bottom?r.yMax:r.y-pin;
                for(int i=0;i<n;i++){float lx=inset+start+i*LegPitch,lit=calm?1:LegLit(reach,r,C,lx+LegWidth/2,bottom);sum+=lit;if(lit>.02f){if(!any)x0=r.x+lx;x1=r.x+lx+LegWidth;any=true;}}
                if(!any)continue;U.Glow(new Rect(x0,y+pin/2-1,x1-x0,2),U.A(h.Pin,.18f*k*sum/Mathf.Max(1,n)),5);
                for(int i=0;i<n;i++)
                {
                    float lx=inset+start+i*LegPitch,cx=lx+LegWidth/2,lit=calm?1:LegLit(reach,r,C,cx,bottom);if(lit<=.02f)continue;
                    float bump=0;if(comet){float s=bottom?2*(r.width-C)+(r.height-C)+C*Root2-cx:cx-C,d=Mathf.Repeat(s-hc,L);bump=Mathf.Max(0,1-Mathf.Min(d,L-d)/12);}
                    U.Fill(new Rect(r.x+lx,bottom?y:y+1,LegWidth,pin-1),U.A(hot,Mathf.Min(1,(.32f*lit+.4f*bump)*k)));
                }
            }
        }
        // The scan: faint 3 px holo lines while lit (Siren has its own) and the line's one pass down the face, a trailing sheen
        // above it, clipped to the face clear of the cuts; calm: a still sheen down from the top edge instead of the pass.
        public static void HoloScan(Rect r,float radius,string tier,Color rare,float k,float age,bool calm)
        {
            var h=HoloOf(tier,rare);Ensure();float C=Snap(CutOf(radius));var face=new Rect(r.x+1.5f,r.y+C,r.width-3,r.height-2*C);if(!h.Glitch)U.Tile(face,scan,U.A(h.Scan,.03f*k),new Vector2(1,3));
            if(calm){U.Fade(new Rect(face.x,face.y,face.width,Mathf.Min(60,face.height*.3f)),U.A(h.Scan,.05f*k));return;}
            if(!NativeHoloScan.ScanPass(age,calm,out float e))return;float y=face.y-14+(face.height+28)*e,lw=face.width,th=NativeHudChrome.Px;
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
            if(op<=.002f)return;op=Mathf.Min(1,op);const float th=2,tick=3.5f;float ax=Mathf.Min(18,r.width*.14f),ay=Mathf.Min(26,r.height*.16f),key=NativeHudChrome.Px;
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
            if(age<0||age>=NativeHoloScan.Lock||a<=0)return;var h=HoloOf(tier,rare);float u=age/NativeHoloScan.Lock,op=(1-u)*a,cut=CutOf(radius);
            Brackets(r,h,NativeHoloScan.LockOffset(age,calm),op,0,true);ChamferEdge(r,U.A(h.Head,(calm?.5f:.9f)*op),cut,1.5f);if(!calm)Chamfer(r,U.A(Color.white,.14f*(1-u)*(1-u)*a),cut);
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
