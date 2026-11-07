using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using G=ChipArena.UnityConversion.NativeLoadoutGrid;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The loadout pickers' cyber tiles (NativeLoadoutUI): a translucent navy plate over a faint hex lattice (anchored to the
    // canvas, so neighbouring tiles read as one surface) with 3 px holo lines, a slow holo scan that travels across the grid as
    // a wave, a soft hex shimmer band and circuit traces in the corner. Hover: a bright scan pass, a chromatic edge flicker,
    // the icon lifting and a cyan edge. Selected: a cyan edge with its glow, corner brackets that snap on and breathe, a lock
    // flash, a data-pulse ring from the icon and the icon's glow in the weapon's colour. The DEV class keeps its mystery
    // (violet, a glitching name); restricted DEV weapons carry a violet pip. Calm (Reduced effects): still, no flicker.
    // Timeline and layout: Core/NativeLoadoutGrid. Textures are built on first use, never in field initializers; drawing
    // allocates nothing. Nothing is transformed (no U.Push): the tiles draw inside scroll clips.
    public static class NativeCyberTiles
    {
        public struct Look
        {
            // Alpha: the panel fade times the entrance. Hover / Selected 0..1; their ages in seconds since the pointer arrived and
            // the pick (or the entrance's lock); Time the screen clock; Col / Row the tile's place (the scan wave).
            public float Alpha,Hover,HoverAge,Selected,SelectAge,Time;public int Col,Row;public bool Calm,Mystery,Dev;public Color Accent;
        }
        private const float Radius=6,HexS=7;
        private static readonly Vector2 HexPeriod=new Vector2(1.7320508f*HexS,3*HexS),LinePeriod=new Vector2(1,3);
        private static readonly float[] RingAt={0,.8f,.88f,.94f,1},RingA={0,0,1,.35f,0},ShimmerA={.35f,1,1,.35f};
        private static readonly Color TopRest=U.Hex(0x111936),TopHot=U.Hex(0x18244e),TopOn=U.Hex(0x0b3049),BottomRest=U.Hex(0x070b18),BottomOn=U.Hex(0x05192a);
        private static readonly Color MysteryTop=U.Hex(0x1c1438),MysteryBottom=U.Hex(0x0c0818),Keyline=U.Hex(0x03050b);
        // Circuit traces for the bottom-right corner (100 x 100 box): three buses turning into the corner, two pads and a via.
        private const string Traces="<path d=\"M100 58H84l-10 10v32M100 70H90l-6 6v24M100 82h-5M62 100V92l-8-8H38M48 100v-4l-4-4H26\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.5\"/>"
            +"<circle cx=\"35\" cy=\"84\" r=\"2.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.4\"/><circle cx=\"23\" cy=\"92\" r=\"2.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.4\"/><circle cx=\"92\" cy=\"82\" r=\"2.1\" fill=\"#fff\"/>";
        private static Texture2D hex,lines;
        private static Texture2D Make(int w,int h){return new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};}
        private static void Ensure()
        {
            if(hex!=null)return;
            // A pointy-top hex lattice, circumradius 1: one period is sqrt 3 wide and 3 tall (two offset rows of centres); the
            // texel's distance to its hex's edge (the smaller hex metric of the two rows) drawn as a soft ~1.4 px line.
            const int hw=48,hh=84;const float r3=1.7320508f;hex=Make(hw,hh);var px=new Color[hw*hh];
            for(int y=0;y<hh;y++)for(int x=0;x<hw;x++){float ux=(x+.5f)/hw*r3,uy=(y+.5f)/hh*3,d=Mathf.Min(HexMetric(ux,uy),HexMetric(ux+r3/2,uy+1.5f));px[y*hw+x]=new Color(1,1,1,Mathf.Clamp01(1-Mathf.Abs(d-r3/2)/.1f));}
            hex.SetPixels(px);hex.Apply(false,true);
            // Holo lines: a 1 px line every 3 px (4 texels per px; row 0 is the bottom, the line on the top texels).
            lines=Make(1,12);px=new Color[12];for(int y=0;y<12;y++)px[y]=new Color(1,1,1,y>=8?1:0);lines.SetPixels(px);lines.Apply(false,true);
        }
        private static float HexMetric(float x,float y){const float r3=1.7320508f;float qx=Mathf.Abs(Mathf.Repeat(x,r3)-r3/2),qy=Mathf.Abs(Mathf.Repeat(y,3)-1.5f);return Mathf.Max(qx,.5f*qx+.8660254f*qy);}
        private static float Px(float v)=>Mathf.Max(1,v*U.S)/U.S;
        private static Rect Inset(Rect r,float d)=>new Rect(r.x+d,r.y+d,r.width-2*d,r.height-2*d);
        private static Rect Square(Vector2 c,float d)=>new Rect(c.x-d/2,c.y-d/2,d,d);
        private static Color Tone(in Look k)=>k.Mystery?C.Violet():C.Cyan();

        // A weapon tile: the icon, large, with the weapon's name (two lines at most) under it.
        public static void Weapon(Rect r,in Look k,string icon,string name)
        {
            if(k.Alpha<=.002f||Event.current.type!=EventType.Repaint)return;Ensure();var tone=Tone(k);Plate(r,k,tone);
            G.WeaponFace(r.width,out float size,out float cy,out float nameTop,out float nameH);float lift=G.Lift(k.Hover,k.Selected,k.Calm);
            var ic=new Rect(r.center.x-size/2,r.y+cy-size/2-lift,size,size);Halo(r,ic,k,tone);Icon(ic,icon,k,lift,0);
            float ns=G.NameSize(r.width),lh=ns*G.LineHeight;var lines=U.Wrap(name,ns,r.width-12,true,G.NameLines);float y=r.y+nameTop+(nameH-lines.Count*lh)/2;var ink=Ink(k);
            for(int i=0;i<lines.Count;i++){U.Text(new Rect(r.x+3,y,r.width-6,lh),lines[i],ns,ink,TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,.5f*k.Alpha));y+=lh;}
            Edge(r,k,tone);
        }
        // A class tile: the class icon over its name (uppercase, tracked); the DEV class's name glitches on its beat.
        public static void Class(Rect r,in Look k,G.Row row,string icon,string label)
        {
            if(k.Alpha<=.002f||Event.current.type!=EventType.Repaint)return;Ensure();var tone=Tone(k);Plate(r,k,tone);
            G.ClassFace(row,r.height,out float size,out float cy,out float nameTop,out float nameH);float lift=G.Lift(k.Hover,k.Selected,k.Calm)*.6f,beat=k.Mystery?G.Glitch(k.Time,k.Calm):-1;
            float jolt=beat>=.65f&&beat<.72f?(beat<.685f?-1.5f:1):0;var ic=new Rect(r.center.x-size/2,r.y+cy-size/2-lift,size,size);Halo(r,ic,k,tone);Icon(ic,icon,k,lift,jolt);
            float ns=G.ClassNameSize(row);var line=new Rect(r.x+3,r.y+nameTop,r.width-6,nameH);
            U.Text(line,label,ns,Ink(k),TextAnchor.MiddleCenter,bold:true,tracking:G.ClassTracking,shadow:new Color(0,0,0,.5f*k.Alpha));
            if(beat>=0){U.SplitCopy(line,line,beat,false,label,ns,k.Alpha,TextAnchor.MiddleCenter,G.ClassTracking);U.SplitCopy(line,line,beat,true,label,ns,k.Alpha,TextAnchor.MiddleCenter,G.ClassTracking);}
            Edge(r,k,tone);
        }

        // ---- layers ----------------------------------------------------------------------------------------------------
        // Under the content: the glow, the plate, then (clipped to its face) the lattice, its shimmer, the holo lines, the
        // traces and the scan.
        private static void Plate(Rect r,in Look k,Color tone)
        {
            float a=k.Alpha,h=k.Hover,s=k.Selected;
            if(s>0)U.Glow(r,U.A(tone,.16f*s*a*G.Breath(k.Time,k.Calm)),10);if(h*(1-s)>0)U.Glow(r,U.A(tone,.07f*h*(1-s)*a),8);
            Color top=k.Mystery?U.Mix(MysteryTop,U.Hex(0x2a1a55),.5f*h+.6f*s):U.Mix(U.Mix(TopRest,TopHot,h),TopOn,s),bottom=k.Mystery?MysteryBottom:U.Mix(BottomRest,BottomOn,s);
            U.RoundGradient(r,U.A(top,.92f*a),U.A(bottom,.95f*a),Radius);U.HGradient(new Rect(r.x+Radius,r.y+1,(r.width-2*Radius)/2,Px(1)),Color.clear,new Color(1,1,1,(.07f+.06f*h)*a));U.HGradient(new Rect(r.center.x,r.y+1,(r.width-2*Radius)/2,Px(1)),new Color(1,1,1,(.07f+.06f*h)*a),Color.clear);
            var face=Inset(r,1);U.BeginBand(face);var phase=new Vector2(Mathf.Repeat(face.x,HexPeriod.x),Mathf.Repeat(face.y,HexPeriod.y));
            U.Tile(face,hex,U.A(tone,(.036f+.03f*h+.04f*s)*a),HexPeriod,phase);
            if(G.Shimmer(k.Time,k.Col,k.Row,k.Calm,out float centre))
            {   // A band half the face wide in four steps, the lattice drawn again inside each.
                float bw=face.width*.5f,x0=face.x+face.width*centre-bw/2;
                for(int i=0;i<4;i++){var band=Rect.MinMaxRect(Mathf.Max(face.x,x0+bw*i/4),face.y,Mathf.Min(face.xMax,x0+bw*(i+1)/4),face.yMax);if(band.width<=0)continue;U.BeginBand(band);U.Tile(face,hex,U.A(tone,ShimmerA[i]*(.08f+.06f*h)*a),HexPeriod,phase);U.EndBand();}
            }
            U.Tile(face,lines,U.A(tone,.035f*a),LinePeriod);if(s>0)U.Fill(face,U.A(tone,.03f*s*a));
            float ts=Mathf.Min(face.width,face.height)*.72f;NativeIcons.DrawMarkup(new Rect(face.xMax-ts,face.yMax-ts,ts,ts),"loadout-traces",Traces,100,U.A(tone,(.13f+.12f*h+.17f*s)*a));
            if(k.Calm)U.Fade(new Rect(face.x,face.y,face.width,face.height*.3f),U.A(tone,(.05f+.04f*h)*a));
            else
            {
                if(G.Scan(k.Time,k.Col,k.Row,false,out float p))ScanLine(face,p,(.12f+.12f*h+.06f*s)*a,tone,18);
                if(G.HoverScan(k.HoverAge,false,out float q))ScanLine(face,q,.42f*h*a,tone,28);
            }
            U.EndBand();
        }
        // A scan line `p` of the way down the face (a little past both edges): a 1 px line bright in the middle, a trail above.
        private static void ScanLine(Rect face,float p,float a,Color tone,float trail)
        {
            float y=face.y-6+(face.height+12)*p,th=Px(1),w=face.width;var head=U.A(U.Mix(tone,Color.white,.55f),a);
            U.Fade(new Rect(face.x,y-trail,w,trail),U.A(tone,.45f*a),false);
            U.HGradient(new Rect(face.x,y,w*.3f,th),Color.clear,head);U.Fill(new Rect(face.x+w*.3f,y,w*.4f,th),head);U.HGradient(new Rect(face.x+w*.7f,y,w*.3f,th),head,Color.clear);
        }
        // Behind the icon, inside the tile's face: its glow in the weapon's colour (selected; faint on hover), the data pulse
        // ring and the lock's ring.
        private static void Halo(Rect r,Rect ic,in Look k,Color tone)
        {
            float a=k.Alpha,h=k.Hover,s=k.Selected;if(s<=0&&h<=0)return;U.BeginBand(Inset(r,1));
            if(s>0)U.Radial(Inset(ic,-ic.width*.6f),U.A(k.Accent,.30f*s*a*G.Breath(k.Time,k.Calm)));if(h*(1-s)>0)U.Radial(Inset(ic,-ic.width*.45f),U.A(k.Accent,.12f*h*(1-s)*a));
            if(s>0&&G.Pulse(k.SelectAge,k.Calm,out float grow,out float pa))U.RadialStops(Square(ic.center,ic.width*(1.05f+1.15f*grow)),U.A(tone,pa*s*a),RingAt,RingA);
            float lf=G.LockFlash(k.SelectAge,k.Calm)*s;if(lf>0)U.RadialStops(Square(ic.center,ic.width*(1.1f+1.4f*(1-lf))),U.A(U.Mix(tone,Color.white,.4f),.6f*lf*a),RingAt,RingA);
            U.EndBand();
        }
        // The icon: a drop shadow that stays as it lifts, the chromatic split while the edge flickers (or the glitch's jolt),
        // then the icon itself: pale with a hint of the weapon's colour at rest, brighter on hover, in its colour selected.
        private static void Icon(Rect ic,string icon,in Look k,float lift,float jolt)
        {
            float a=k.Alpha,h=k.Hover,s=k.Selected;NativeIcons.Draw(new Rect(ic.x,ic.y+2+lift*.6f,ic.width,ic.height),icon,new Color(0,0,0,.5f*a));
            float f=G.Flicker(k.HoverAge,k.Calm)*Mathf.Clamp01(3*h),dx=1.6f*f+Mathf.Abs(jolt);
            if(dx>0){float o=f>0?.5f*f:.6f;NativeIcons.Draw(new Rect(ic.x-dx,ic.y,ic.width,ic.height),icon,C.Magenta(o*a));NativeIcons.Draw(new Rect(ic.x+dx,ic.y,ic.width,ic.height),icon,C.Cyan(o*a));}
            Color rest=U.A(U.Mix(C.Ink(),k.Accent,.26f),.8f),hot=U.Mix(C.Ink(),k.Accent,.18f),on=U.Mix(k.Accent,Color.white,.42f);
            NativeIcons.Draw(new Rect(ic.x+jolt,ic.y,ic.width,ic.height),icon,U.A(U.Mix(U.Mix(rest,hot,h),on,s),a));
        }
        private static Color Ink(in Look k)=>U.A(U.Mix(U.Mix(C.Muted(),C.Ink(),k.Hover),C.Ink(),k.Selected),k.Alpha);
        // Over the content: the edge (cyan selected) and its chromatic flicker, the inner corner ticks (unselected), the DEV pip,
        // the selection's brackets and the lock flash.
        private static void Edge(Rect r,in Look k,Color tone)
        {
            float a=k.Alpha,h=k.Hover,s=k.Selected;Color rest=k.Mystery?U.Mix(C.Edge(),C.Violet(),.4f):C.Edge(),hot=U.Mix(rest,U.Mix(tone,Color.white,.35f),.6f);
            U.Round(r,Color.clear,Radius,U.A(U.Mix(U.Mix(rest,hot,h),tone,s),a),s>.5f?1.5f:1);
            float f=G.Flicker(k.HoverAge,k.Calm)*Mathf.Clamp01(3*h);
            if(f>0){U.Round(new Rect(r.x-1.5f,r.y,r.width,r.height),Color.clear,Radius,C.Magenta(.55f*f*a));U.Round(new Rect(r.x+1.5f,r.y,r.width,r.height),Color.clear,Radius,C.Cyan(.55f*f*a));}
            float tick=(.3f+.45f*h)*(1-s)*a;if(tick>0)Ticks(r,U.A(tone,tick));
            if(k.Dev&&!k.Mystery){var d=new Rect(r.xMax-11,r.y+7,4,4);U.Glow(d,C.Violet(.5f*a),4);U.Fill(d,U.A(U.Mix(C.Violet(),Color.white,.35f),a));}
            if(s>0)Brackets(r,G.BracketOffset(k.SelectAge,k.Time,k.Calm),s*a*G.BracketOpacity(k.SelectAge,k.Calm),tone);
            float lf=G.LockFlash(k.SelectAge,k.Calm)*s;if(lf>0){U.Round(r,new Color(1,1,1,.10f*lf*a),Radius);U.Round(r,Color.clear,Radius,U.A(U.Mix(tone,Color.white,.6f),lf*a),1.5f);}
        }
        // Small L ticks inside the top-left and bottom-right corners.
        private static void Ticks(Rect r,Color c)
        {
            float th=Px(1.5f),arm=Mathf.Min(8,r.width*.1f),i=5;
            U.Fill(new Rect(r.x+i,r.y+i,arm,th),c);U.Fill(new Rect(r.x+i,r.y+i+th,th,arm-th),c);
            U.Fill(new Rect(r.xMax-i-arm,r.yMax-i-th,arm,th),c);U.Fill(new Rect(r.xMax-i-th,r.yMax-i-arm,th,arm-th),c);
        }
        // Four square-ended L brackets `off` outside the corners, each over a dark keyline with a soft glow (as the draft tray's).
        private static void Brackets(Rect r,float off,float op,Color tone)
        {
            if(op<=.002f)return;op=Mathf.Min(1,op);const float th=2;float ax=Mathf.Min(14,r.width*.16f),ay=Mathf.Min(14,r.height*.2f),key=Px(1);var col=U.Mix(tone,Color.white,.35f);
            for(int c=0;c<4;c++)
            {
                bool right=c==1||c==2,bottom=c>=2;float px=right?r.xMax+off:r.x-off,py=bottom?r.yMax+off:r.y-off;
                var arm=new Rect(right?px-ax:px,bottom?py-th:py,ax,th);var leg=new Rect(right?px-th:px,bottom?py-ay:py+th,th,ay-th);
                U.Fill(new Rect(arm.x-key,arm.y-key,arm.width+2*key,arm.height+2*key),U.A(Keyline,.55f*op));U.Fill(new Rect(leg.x-key,bottom?leg.y-key:leg.y+key,leg.width+2*key,leg.height),U.A(Keyline,.55f*op));
                U.Glow(arm,U.A(tone,.32f*op),5);U.Glow(leg,U.A(tone,.32f*op),5);U.Fill(arm,U.A(col,op));U.Fill(leg,U.A(col,op));
            }
        }

        // The info strip under the weapon tiles: the shown weapon's name and role, and its key stats in mono. `live` 0..1 is a
        // hovered weapon (a cyan edge, the notch over its tile at `notch`); at 0 the strip rests, dimmed, on the equipped one
        // (`equipped`: the check before the name). `fade` cross-fades the content when the shown weapon changes.
        public static void Strip(Rect s,string name,string role,string stats,bool equipped,float live,float fade,float notch,float a)
        {
            if(a<=.002f||Event.current.type!=EventType.Repaint)return;var tone=C.Cyan();
            U.Round(s,C.Navy((.4f+.3f*live)*a),Radius,U.A(U.Mix(C.Edge(),tone,.5f*live),(.55f+.4f*live)*a));U.Fill(new Rect(s.x+1,s.y+9,Px(2),s.height-18),U.A(tone,(.3f+.7f*live)*a));
            if(live>0){float nx=Mathf.Clamp(notch,s.x+16,s.xMax-16);var n=new Rect(nx-10,s.y-1,20,Px(2));U.Glow(n,U.A(tone,.45f*live*a),5);U.Fill(n,U.A(U.Mix(tone,Color.white,.3f),live*a));}
            float k=a*fade,x=s.x+16,w=s.width-32;if(k<=.002f||string.IsNullOrEmpty(name))return;
            if(equipped)NativeIcons.Draw(new Rect(x,s.y+8,13,13),"check",U.A(tone,(.6f+.4f*live)*k));float tx=x+(equipped?19:0),tw=w-(equipped?19:0);
            float nw=Mathf.Min(U.Measure(name,13,true).x,tw);U.Text(new Rect(tx,s.y+5,nw+2,19),name,13,U.A(C.Ink(),(.72f+.28f*live)*k),TextAnchor.MiddleLeft,bold:true);
            var r=U.Wrap(role,12,Mathf.Max(10,tw-nw-14),false,1);if(r.Count>0)U.Text(new Rect(tx+nw+12,s.y+5,tw-nw-12,19),r[0],12,U.A(C.Muted(),(.8f+.2f*live)*k),TextAnchor.MiddleLeft);
            U.Text(new Rect(x,s.y+24,w+8,15),stats,10.5f,U.A(U.Mix(C.Dim(),C.CyanSoft(),.45f+.55f*live),k),TextAnchor.MiddleLeft,mono:true,tracking:.08f);
        }
    }
}
