using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // Portrait backdrops (Core/NativePortraitBacks.cs): the effect behind the pilot that PortraitStage draws before the
    // portrait on the home pilot card, the lobby and results cards and the Locker turntable, and the Locker's BACKDROP cards
    // (a live preview behind a voxel bust). The Tick Ring stays PortraitRing (NativePortraitUI.cs); every other style is here.
    // Clip rule: the lobby roster's cards sit in a GUI scroll clip, where a rotated or scaled draw carries the clip with it.
    // So every drawer below makes only unrotated, unscaled draws on every path: radial gradients, fills, and positioned
    // images of textures baked once per kit scale (rays, streaks and outlines are baked at each angle they are shown at
    // rather than turned). Each style keeps inside the stage rect: glows take the room to the nearer edges, dots shrink as
    // they near an edge, and an image overlapping an edge is cut there with its texture coordinates (BkImage). Drawing is
    // behind the pilot by order (the portrait goes over it), so nothing covers the face.
    // A frame allocates nothing: stop arrays are static, textures are cached by an integer key and lookups do not box.
    public sealed partial class ConversionGame
    {
        private static readonly float[] BkSoftAt={0f,.5f,1f},BkSoftA={1f,.42f,0f},BkCoreAt={0f,.55f,1f},BkCoreA={1f,.35f,0f},BkDotAt={0f,.3f,1f},BkDotA={1f,.5f,0f},
            BkHotAt={0f,.55f,1f},BkHotA={1f,.9f,0f},BkPadAt={0f,.28f,.42f,.62f,.74f,.88f,1f},BkPadA={1f,1f,0f,0f,1f,1f,0f},BkFlatAt={0f,.7f,1f},BkFlatA={1f,.3f,0f};
        private static readonly Color BkCyan=new Color(.18f,.89f,1f,1f),BkMagenta=new Color(1f,.18f,.46f,1f);

        // ---- Dispatch ----------------------------------------------------------------------------------------------------
        // `c` and `ring` are the portrait ring's centre and radius in the stage `s` (PortraitStage's framing), `t` the card's time
        // (with its phase), `head` how far above the ring centre the head sits (ring radii).
        private static void PortraitBack(int style,Rect s,Vector2 c,float ring,Color accent,Color body,float e,float t,bool reduced,float head)
        {
            if(Event.current.type!=EventType.Repaint||ring<4||s.width<8||s.height<8)return;float tm=NativePortraitBacks.Clock(t,reduced);
            // A margin inside the stage keeps every style clear of the stage's frame and corner labels (the Locker's LIVE
            // PREVIEW / TRYING ON, a card's top row).
            float m=Mathf.Clamp(s.height*.055f,2,30);s=new Rect(s.x+m,s.y+m,s.width-2*m,s.height-2*m);if(s.width<8||s.height<8)return;
            switch(style)
            {
                case NativePortraitBacks.CleanGlow:BkCleanGlow(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.Sunburst:BkSunburst(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.HexField:BkHexField(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.Circuit:BkCircuit(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.Equalizer:BkEqualizer(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.Warp:BkWarp(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.Embers:BkEmbers(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.Orbitals:BkOrbitals(s,c,ring,accent,body,e,tm);break;
                case NativePortraitBacks.Glitch:BkGlitch(s,c,ring,accent,body,e,t,reduced);break;
                case NativePortraitBacks.CrownHalo:BkCrownHalo(s,c,ring,accent,body,e,tm,head);break;
                case NativePortraitBacks.NeonDiamond:BkNeonDiamond(s,c,ring,accent,body,e,tm);break;
            }
        }

        // ---- Kit: stage-bounded draws --------------------------------------------------------------------------------------
        private static float BkRoomX(Rect s,Vector2 c)=>Mathf.Min(c.x-s.x,s.xMax-c.x);
        private static float BkRoomY(Rect s,Vector2 c)=>Mathf.Min(c.y-s.y,s.yMax-c.y);
        // A radial gradient about `p`, its half-axes held to the room between `p` and the stage's nearer edges.
        private static void BkGlow(Rect s,Vector2 p,float rx,float ry,Color col,float[] at,float[] a)
        {
            rx=Mathf.Min(rx,BkRoomX(s,p));ry=Mathf.Min(ry,BkRoomY(s,p));if(rx<1.5f||ry<1.5f||col.a<=0)return;U.RadialStops(new Rect(p.x-rx,p.y-ry,rx*2,ry*2),col,at,a);
        }
        // A positioned image cut to the stage: the part inside is drawn with the matching texture coordinates (U.Tile with
        // one period the size of the image).
        private static void BkImage(Rect s,Rect r,Texture t,Color col)
        {
            if(t==null||col.a<=0)return;float x0=Mathf.Max(r.x,s.x),y0=Mathf.Max(r.y,s.y),x1=Mathf.Min(r.xMax,s.xMax),y1=Mathf.Min(r.yMax,s.yMax);if(x1-x0<.5f||y1-y0<.5f)return;
            if(x0<=r.x&&y0<=r.y&&x1>=r.xMax&&y1>=r.yMax){U.Image(r,t,col);return;}
            U.Tile(new Rect(x0,y0,x1-x0,y1-y0),t,col,new Vector2(r.width,r.height),new Vector2(x0-r.x,y0-r.y));
        }
        private static void BkFill(Rect s,Rect r,Color col)
        {
            float x0=Mathf.Max(r.x,s.x),y0=Mathf.Max(r.y,s.y),x1=Mathf.Min(r.xMax,s.xMax),y1=Mathf.Min(r.yMax,s.yMax);if(x1-x0>.05f&&y1-y0>.05f)U.Fill(new Rect(x0,y0,x1-x0,y1-y0),col);
        }
        // A vertical fade (U.Fade) cut to the stage; the visible part keeps its place on the ramp (the opaque end's alpha
        // scales by how much of the ramp the cut left).
        private static void BkFade(Rect s,Rect r,Color col,bool fromTop)
        {
            float x0=Mathf.Max(r.x,s.x),x1=Mathf.Min(r.xMax,s.xMax),y0=Mathf.Max(r.y,s.y),y1=Mathf.Min(r.yMax,s.yMax);if(x1-x0<.05f||y1-y0<.05f||r.height<=0)return;
            float cut=fromTop?(y0-r.y)/r.height:(r.yMax-y1)/r.height;U.Fade(new Rect(x0,y0,x1-x0,y1-y0),U.A(col,1-cut),fromTop);
        }
        // A soft dot: a glow `size`*2.6 across and a hot core `size` across, shrinking as it nears a stage edge.
        private static void BkDot(Rect s,Vector2 p,float size,Color glow,Color core)
        {
            float edge=Mathf.Min(Mathf.Min(p.x-s.x,s.xMax-p.x),Mathf.Min(p.y-s.y,s.yMax-p.y));if(edge<=.5f)return;
            float g=Mathf.Min(size*1.3f,edge),k=Mathf.Min(size*.5f,edge);
            if(glow.a>0)U.RadialStops(new Rect(p.x-g,p.y-g,g*2,g*2),glow,BkDotAt,BkDotA);if(core.a>0)U.RadialStops(new Rect(p.x-k,p.y-k,k*2,k*2),core,BkHotAt,BkHotA);
        }
        private static Color BkHot(Color accent,float k)=>U.Mix(accent,Color.white,k);
        private static Color BkShade(Color c,float k)=>new Color(c.r*k,c.g*k,c.b*k,c.a);
        // The neon line width (units) for a ring radius: thin on a card, a little bolder on the large stage.
        private static float BkLine(float ring)=>Mathf.Clamp(ring*.012f,1.15f,2.6f);

        // ---- Kit: baked textures -------------------------------------------------------------------------------------------
        // Baked once per kit scale (made again when U.S changes) and per pixel size: an image `units` wide is baked about as
        // many pixels as it covers (rounded up to 16, at most 512), so its lines stay crisp on a card and on the large stage.
        private const int BkSun=1,BkHexLine=2,BkHexFill=3,BkStreak=4,BkEllipse=5,BkDiamond=6,BkRing=7,BkScan=8;
        private static readonly Dictionary<ulong,Texture2D> backTextures=new Dictionary<ulong,Texture2D>();private static float backScale;
        private static int BkPx(float units,int cap=512)=>Mathf.Clamp(Mathf.CeilToInt(units*U.S/16f)*16,16,cap);
        private static Texture2D BackTexture(int kind,int variant,int px,float linePx=0,float glowPx=0)
        {
            if(backScale!=U.S||backTextures.Count>192){backScale=U.S;foreach(var old in backTextures.Values)if(old!=null)Object.Destroy(old);backTextures.Clear();}
            int l=Mathf.Clamp(Mathf.RoundToInt(linePx*4),0,4095),g=Mathf.Clamp(Mathf.RoundToInt(glowPx*2),0,4095);
            ulong key=(ulong)(uint)kind<<56|(ulong)(uint)(variant&0xffff)<<40|(ulong)(uint)(px&0xfff)<<28|(ulong)(uint)l<<14|(uint)g;
            if(backTextures.TryGetValue(key,out var t)&&t!=null)return t;t=BakeBack(kind,variant,px,l/4f,g/2f);backTextures[key]=t;return t;
        }
        private static Texture2D BakeBack(int kind,int variant,int px,float linePx,float glowPx)
        {
            // The scanline tile is 1 opaque row over 2 clear ones (4 by 3 texels, repeated); every other kind is a square.
            int w=kind==BkScan?4:px,h=kind==BkScan?3:px;var colours=new Color[w*h];float half=px*.5f,line=linePx/half,glow=glowPx/half;
            for(int j=0;j<h;j++)for(int i=0;i<w;i++)
            {
                // (x, y) in [-1, 1] with y down (texture row 0 is the bottom).
                float x=(i+.5f)/half-1,y=1-(j+.5f)/half,a;
                switch(kind)
                {
                    case BkSun:a=NativePortraitBacks.SunTexel(x,y,variant);break;
                    case BkHexLine:a=NativePortraitBacks.HexTexel(x,y,line,false);break;
                    case BkHexFill:a=NativePortraitBacks.HexTexel(x,y,line,true);break;
                    case BkStreak:a=NativePortraitBacks.StreakTexel(x,y,variant,.09f);break;
                    case BkEllipse:a=NativePortraitBacks.EllipseTexel(x,y,(variant>>8)/100f,(variant&255)-90,line,glow,.38f);break;
                    case BkDiamond:a=NativePortraitBacks.DiamondTexel(x,y,line,variant==1?0:glow,.42f);break;
                    case BkRing:a=NativePortraitBacks.RingTexel(x,y,line,variant==1?0:glow,.4f);break;
                    case BkScan:a=j==h-1?1:0;break;
                    default:a=0;break;
                }
                // Every square bake fades to nothing at its border, so a glow never ends on a hard texture edge.
                if(kind!=BkScan)a*=NativePortraitBacks.Smooth((1-Mathf.Max(Mathf.Abs(x),Mathf.Abs(y)))/.07f);
                colours[j*w+i]=new Color(1,1,1,a);
            }
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=kind==BkScan?TextureWrapMode.Repeat:TextureWrapMode.Clamp,filterMode=kind==BkScan?FilterMode.Point:FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            t.SetPixels(colours);t.Apply(false,true);return t;
        }
        // An ellipse ring texture: aspect (short over long axis) and tilt in degrees, packed into the variant.
        private static int BkEllipseVariant(float aspect,float tilt)=>Mathf.RoundToInt(aspect*100)<<8|(Mathf.RoundToInt(tilt)+90)&255;

        // ---- Styles ----------------------------------------------------------------------------------------------------------
        // Clean Glow: a soft accent glow, a brighter core behind the chest and a faint rim of light, breathing slowly.
        private static void BkCleanGlow(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            float breathe=.86f+.14f*Mathf.Sin(t*.9f);
            BkGlow(s,c,R*2f,R*1.75f,U.A(accent,.24f*e*breathe),BkSoftAt,BkSoftA);
            BkGlow(s,c,R*1.08f,R*1.08f,U.A(BkHot(accent,.3f),.26f*e*breathe),BkCoreAt,BkCoreA);
            float rr=Mathf.Min(R*1.04f,Mathf.Min(BkRoomX(s,c),BkRoomY(s,c))-3);if(rr<8)return;float size=rr*2/.9f;
            var rim=BackTexture(BkRing,0,BkPx(size),BkLine(R)*BkPx(size)/size,BkLine(R)*5*BkPx(size)/size);
            BkImage(s,new Rect(c.x-size/2,c.y-size/2,size,size),rim,U.A(U.Mix(accent,body,.25f),.22f*e*(.8f+.2f*breathe)));
        }
        // Sunburst: twelve long and short rays turning slowly (two baked turns crossfaded), a finer counter-turning layer in
        // the body colour and a hot core.
        private static void BkSunburst(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            BkGlow(s,c,R*1.9f,R*1.7f,U.A(accent,.13f*e),BkSoftAt,BkSoftA);
            // The rays fill an ellipse held to the room (a squat stage squashes them rather than cutting them at its edge).
            float rx=Mathf.Min(R*1.8f,BkRoomX(s,c)),ry=Mathf.Min(R*1.8f,BkRoomY(s,c));if(rx<R*.5f||ry<R*.5f)return;int px=BkPx(Mathf.Max(rx,ry)*2,256);
            NativePortraitBacks.SunPhase(t*NativePortraitBacks.SunSpeed,out int a,out int b,out float k);var r=new Rect(c.x-rx,c.y-ry,rx*2,ry*2);Color ray=BkHot(accent,.18f);
            BkImage(s,r,BackTexture(BkSun,a,px),U.A(ray,.46f*e*(1-k)));BkImage(s,r,BackTexture(BkSun,b,px),U.A(ray,.46f*e*k));
            float ix=Mathf.Min(R*1.05f,rx),iy=Mathf.Min(R*1.05f,ry);int ipx=BkPx(Mathf.Max(ix,iy)*2,256);var ir=new Rect(c.x-ix,c.y-iy,ix*2,iy*2);
            NativePortraitBacks.SunPhase(-t*NativePortraitBacks.SunSpeed*1.6f+NativePortraitBacks.SunPeriod/4,out a,out b,out k);Color fine=U.Mix(body,accent,.35f);
            BkImage(s,ir,BackTexture(BkSun,a,ipx),U.A(fine,.2f*e*(1-k)));BkImage(s,ir,BackTexture(BkSun,b,ipx),U.A(fine,.2f*e*k));
            BkGlow(s,c,R*.95f,R*.95f,U.A(BkHot(accent,.4f),.3f*e),BkCoreAt,BkCoreA);
        }
        // Hex Field: flat-top neon cells fading out toward the stage, an outward shimmer wave and twinkling lit cells.
        private static void BkHexField(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            BkGlow(s,c,R*1.7f,R*1.5f,U.A(accent,.12f*e),BkSoftAt,BkSoftA);
            float cell=Mathf.Clamp(R*.25f,5.5f,34f),rx=Mathf.Min(R*2.1f,BkRoomX(s,c)-cell),ry=Mathf.Min(R*1.7f,BkRoomY(s,c)-cell);if(rx<cell||ry<cell)return;
            int px=BkPx(cell*2,128);float line=BkLine(R)*.85f*px/(cell*2);var outline=BackTexture(BkHexLine,0,px,line);var lit=BackTexture(BkHexFill,0,px,line);
            Color edge=U.Mix(accent,body,.22f),glow=BkHot(accent,.15f);int cols=Mathf.CeilToInt(rx/(cell*1.5f)),rows=Mathf.CeilToInt(ry/(cell*1.732f))+1;
            for(int col=-cols;col<=cols;col++)for(int row=-rows;row<=rows;row++)
            {
                NativePortraitBacks.HexCentre(col,row,out float hx,out float hy);float x=hx*cell,y=hy*cell,d=Mathf.Sqrt(x*x/(rx*rx)+y*y/(ry*ry));if(d>1)continue;
                float fall=1-NativePortraitBacks.Smooth((d-.45f)/.55f),shimmer=NativePortraitBacks.HexShimmer(col,row,Mathf.Sqrt(x*x+y*y)/R,t);var r=new Rect(c.x+x-cell,c.y+y-cell,cell*2,cell*2);
                BkImage(s,r,outline,U.A(edge,(.25f+.55f*shimmer)*fall*e));if(shimmer>.04f)BkImage(s,r,lit,U.A(glow,.34f*shimmer*fall*e));
            }
        }
        // Circuit: traces from the body out to pads, a via at each bend, and a pulse racing out along each trace that lights
        // its pad as it arrives.
        private static void BkCircuit(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            BkGlow(s,c,R*1.5f,R*1.35f,U.A(accent,.11f*e),BkSoftAt,BkSoftA);
            float lw=Mathf.Clamp(R*.026f,1.1f,3.2f),pad=lw*3.4f;
            float k=R*Mathf.Min(1,Mathf.Min((c.y-s.y-pad)/(.94f*R),Mathf.Min((s.yMax-c.y-pad)/(1.16f*R),(BkRoomX(s,c)-pad)/(1.5f*R))));if(k<R*.4f)return;
            Color trace=U.Mix(accent,body,.15f),hot=BkHot(accent,.6f);
            for(int i=0;i<NativePortraitBacks.Traces;i++)
            {
                int n=NativePortraitBacks.TracePoints(i);NativePortraitBacks.TracePoint(i,0,out float px,out float py);var a=new Vector2(c.x+px*k,c.y+py*k);
                for(int j=1;j<n;j++)
                {
                    NativePortraitBacks.TracePoint(i,j,out px,out py);var b=new Vector2(c.x+px*k,c.y+py*k);
                    BkFill(s,new Rect(Mathf.Min(a.x,b.x)-lw/2,Mathf.Min(a.y,b.y)-lw/2,Mathf.Abs(b.x-a.x)+lw,Mathf.Abs(b.y-a.y)+lw),U.A(trace,.36f*e));
                    if(j<n-1)BkFill(s,new Rect(b.x-lw*1.15f,b.y-lw*1.15f,lw*2.3f,lw*2.3f),U.A(trace,.55f*e));
                    a=b;
                }
                float pulse=NativePortraitBacks.TracePulse(i,t),flash=pulse>1?1-(pulse-1)/(NativePortraitBacks.PulseRest-1):0;
                BkGlow(s,a,pad*(1+.5f*flash),pad*(1+.5f*flash),U.A(U.Mix(trace,hot,flash),(.6f+.4f*flash)*e),BkPadAt,BkPadA);
                if(flash>0)BkGlow(s,a,pad*2.6f,pad*2.6f,U.A(accent,.4f*flash*e),BkDotAt,BkDotA);
                if(pulse>1)continue;
                for(int tail=2;tail>=0;tail--)
                {
                    NativePortraitBacks.TraceAt(i,pulse-tail*.05f,out px,out py);float fade=tail==0?1:tail==1?.45f:.2f;if(pulse-tail*.05f<0)continue;
                    BkDot(s,new Vector2(c.x+px*k,c.y+py*k),lw*(tail==0?4.2f:3f),U.A(accent,.5f*fade*e),U.A(hot,fade*e));
                }
            }
        }
        // Equalizer: a mirrored spectrum behind the pilot, bars bright at their tips with a dimmer reflection below the
        // centre line, kicking to the beat.
        private static void BkEqualizer(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            BkGlow(s,c,R*1.8f,R*1.2f,U.A(accent,.1f*e),BkSoftAt,BkSoftA);
            int n=R<60?17:25;float width=Mathf.Min((BkRoomX(s,c)-2)*2,R*4.6f),pitch=width/n,bw=Mathf.Max(1.2f,pitch*.56f),top=Mathf.Min(c.y-s.y-3,R*1.3f),below=Mathf.Min(s.yMax-c.y-3,R*1.3f)*.7f;
            if(pitch<2||top<6)return;Color bar=U.Mix(accent,body,.12f),hot=BkHot(accent,.55f);float x0=c.x-width/2;
            BkFill(s,new Rect(x0,c.y-.5f,width,1),U.A(accent,.28f*e));
            for(int i=0;i<n;i++)
            {
                float level=NativePortraitBacks.EqLevel(i,n,t),x=x0+pitch*(i+.5f)-bw/2,h=top*(.06f+.94f*level),cap=Mathf.Max(1.2f,bw*.3f);
                BkFade(s,new Rect(x,c.y-h,bw,h),U.A(bar,.62f*e),true);BkFill(s,new Rect(x,c.y-h-cap-1,bw,cap),U.A(hot,.92f*e));
                float hb=Mathf.Min(below,h*.7f);if(hb>1)BkFade(s,new Rect(x,c.y+1,bw,hb),U.A(bar,.22f*e),false);
            }
        }
        // Warp: streaks rushing out along fixed directions, longer, brighter and bolder as they speed up, each running from
        // just outside the pilot to the stage edge in its direction; two faint rings of a tunnel and a hot core.
        private static void BkWarp(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            BkGlow(s,c,R*1.8f,R*1.6f,U.A(accent,.12f*e),BkSoftAt,BkSoftA);
            float max=Mathf.Min(BkRoomX(s,c),BkRoomY(s,c))*.98f;
            if(max>R*.7f)
            {
                float size=max*2/.9f;int px=BkPx(size);var tunnel=BackTexture(BkRing,0,px,BkLine(R)*px/size,BkLine(R)*4*px/size);
                for(int i=0;i<2;i++){float f=NativePortraitBacks.Frac(t*.32f+i*.5f),rr=Mathf.Lerp(R*.65f,max,f*f),sz=rr*2/.9f;BkImage(s,new Rect(c.x-sz/2,c.y-sz/2,sz,sz),tunnel,U.A(U.Mix(accent,body,.3f),.12f*NativePortraitBacks.StreakAlpha(f)*e));}
            }
            int spx=BkPx(R*.75f,64);Color hot=BkHot(accent,.35f);float r0=R*.55f;
            for(int i=0;i<NativePortraitBacks.Streaks;i++)
            {
                int d=NativePortraitBacks.StreakDirection(i);float a=d*(360f/NativePortraitBacks.StreakSteps)*Mathf.Deg2Rad,dx=Mathf.Sin(a),dy=-Mathf.Cos(a);
                float reach=Mathf.Min(dx>1e-4f?(s.xMax-c.x)/dx:dx<-1e-4f?(s.x-c.x)/dx:1e6f,dy>1e-4f?(s.yMax-c.y)/dy:dy<-1e-4f?(s.y-c.y)/dy:1e6f)*.97f;if(reach<r0+4)continue;
                float f=NativePortraitBacks.StreakProgress(i,t),g=f*f,len=Mathf.Min(R*(.12f+.62f*g),(reach-r0)*.6f),mid=Mathf.Min(r0+len/2+(reach-r0-len)*g,reach-len/2),side=len/.94f;
                var p=new Vector2(c.x+dx*mid,c.y+dy*mid);BkImage(s,new Rect(p.x-side/2,p.y-side/2,side,side),BackTexture(BkStreak,d%NativePortraitBacks.StreakAngles,spx),U.A(U.Mix(hot,Color.white,.35f*g),(.55f+.45f*g)*NativePortraitBacks.StreakAlpha(f)*e));
            }
            BkGlow(s,c,R*.9f,R*.9f,U.A(BkHot(accent,.45f),.26f*e),BkCoreAt,BkCoreA);
        }
        // Embers: sparks rising from a warm floor glow to the top of the stage, swaying and flickering.
        private static void BkEmbers(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            BkGlow(s,c,R*1.6f,R*1.5f,U.A(accent,.1f*e),BkSoftAt,BkSoftA);
            float spread=Mathf.Min(BkRoomX(s,c)-3,R*1.75f),floor=Mathf.Min(c.y+R*1.5f,s.yMax-4),ceiling=s.y+4,unit=Mathf.Clamp(R*.06f,1.7f,7.5f);if(floor-ceiling<12||spread<6)return;
            BkGlow(s,new Vector2(c.x,floor),spread*.95f,Mathf.Min(R*.3f,(s.yMax-floor)+R*.3f),U.A(accent,.2f*e),BkFlatAt,BkFlatA);
            Color hot=BkHot(accent,.62f),warm=U.Mix(accent,body,.1f);
            for(int i=0;i<NativePortraitBacks.EmberCount;i++)
            {
                NativePortraitBacks.Ember(i,t,out float x,out float rise,out float size,out float alpha);if(alpha<=.01f)continue;
                BkDot(s,new Vector2(c.x+x*spread,Mathf.Lerp(floor,ceiling,rise)),unit*size*2.4f,U.A(warm,.42f*alpha*e),U.A(hot,.95f*alpha*e));
            }
        }
        // Orbitals: three tilted neon orbits round the upper body, two electrons on each with a short trail, and a nucleus glow.
        private static void BkOrbitals(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            float hh=0,hw=0;for(int o=0;o<3;o++){hh=Mathf.Max(hh,NativePortraitBacks.OrbitHalfHeight(o));hw=Mathf.Max(hw,NativePortraitBacks.OrbitHalfWidth(o));}
            float k=Mathf.Min(R,Mathf.Min((BkRoomY(s,c)-5)/hh,(BkRoomX(s,c)-5)/hw));if(k<R*.35f)return;
            BkGlow(s,c,R*1.6f,R*1.4f,U.A(accent,.11f*e),BkSoftAt,BkSoftA);
            float size=NativePortraitBacks.OrbitA*k*2/.9f;int px=BkPx(size);float line=BkLine(R)*px/size,glow=line*3.2f;var r=new Rect(c.x-size/2,c.y-size/2,size,size);
            Color orbit=U.Mix(accent,body,.2f),hot=BkHot(accent,.6f);float dot=Mathf.Clamp(R*.07f,2.2f,9f);
            for(int o=0;o<3;o++)BkImage(s,r,BackTexture(BkEllipse,BkEllipseVariant(NativePortraitBacks.OrbitB/NativePortraitBacks.OrbitA,NativePortraitBacks.OrbitTilt[o]),px,line,glow),U.A(orbit,.42f*e));
            for(int o=0;o<3;o++)for(int j=0;j<2;j++)
            {
                float theta=NativePortraitBacks.ElectronAngle(o,j,t),dir=NativePortraitBacks.OrbitDirection(o);
                for(int tail=3;tail>=0;tail--)
                {
                    NativePortraitBacks.OrbitPoint(o,theta-dir*tail*.16f,out float x,out float y);float fade=tail==0?1:.5f-.13f*tail,depth=.82f+.18f*Mathf.Sin(theta);
                    BkDot(s,new Vector2(c.x+x*k,c.y+y*k),dot*depth*(tail==0?1:.7f),U.A(accent,.5f*fade*e),U.A(hot,fade*e));
                }
            }
            BkGlow(s,c,R*.75f,R*.75f,U.A(BkHot(accent,.4f),.28f*e),BkCoreAt,BkCoreA);
        }
        // Glitch: a scanlined panel behind the pilot with RGB-split corner brackets, a tracking line, and bursts of displaced
        // bands and noise blocks (reduced effects: the bands hold still and faint).
        private static void BkGlitch(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t,bool reduced)
        {
            float w=Mathf.Min((BkRoomX(s,c)-3)*2,R*2.15f),h=Mathf.Min((BkRoomY(s,c)-3)*2,R*1.95f);if(w<16||h<16)return;var p=new Rect(c.x-w/2,c.y-h/2,w,h);
            Color cyan=U.Mix(accent,BkCyan,.55f),magenta=U.Mix(accent,BkMagenta,.55f);float tm=NativePortraitBacks.Clock(t,reduced);
            U.Fill(p,U.A(accent,.045f*e));BkGlow(s,c,w*.5f,h*.5f,U.A(accent,.1f*e),BkSoftAt,BkSoftA);
            U.Tile(p,BackTexture(BkScan,0,4),U.A(U.Mix(accent,body,.3f),.16f*e),new Vector2(3,3),new Vector2(0,NativePortraitBacks.ScanScroll(t,reduced)*-3));
            float track=p.y+NativePortraitBacks.Frac(tm*.22f)*h;U.Fill(new Rect(p.x,track,w,Mathf.Max(1,R*.012f)),U.A(BkHot(accent,.4f),.22f*e));
            bool burst=false;for(int i=0;i<NativePortraitBacks.GlitchBands;i++){NativePortraitBacks.GlitchBand(i,t,reduced,out float y,out float bh,out float dx,out float a);if(a<=0)continue;burst|=!reduced;
                float by=c.y+y*h/2,band=Mathf.Max(1.5f,bh*h/2),shift=dx*w/2;
                BkFill(s,new Rect(p.x+shift,by-band/2,w,band),U.A(cyan,.28f*a*e));BkFill(s,new Rect(p.x-shift*.7f,by-band/2+band*.3f,w,band*.6f),U.A(magenta,.28f*a*e));}
            float split=Mathf.Clamp(R*.03f,1.2f,4f)*(burst?2.2f:1),lw=Mathf.Clamp(R*.024f,1.2f,3f),arm=Mathf.Min(w,h)*.17f;
            BkBrackets(s,new Rect(p.x-split,p.y,w,h),arm,lw,U.A(cyan,.45f*e));BkBrackets(s,new Rect(p.x+split,p.y,w,h),arm,lw,U.A(magenta,.45f*e));BkBrackets(s,p,arm,lw,U.A(BkHot(accent,.3f),.85f*e));
            if(!burst)return;int step=Mathf.FloorToInt(t*NativePortraitBacks.GlitchRate);float block=Mathf.Clamp(R*.05f,2,8);
            for(int i=0;i<6;i++){float bx=NativePortraitBacks.Hash(i*53+step,81),by=NativePortraitBacks.Hash(i*29+step,83);if(NativePortraitBacks.Hash(i+step*13,85)<.4f)continue;
                BkFill(s,new Rect(p.x+bx*(w-block*2),p.y+by*(h-block),block*(1+NativePortraitBacks.Hash(i,87)*2),block*.6f),U.A(i%2==0?cyan:magenta,.55f*e));}
        }
        private static void BkBrackets(Rect s,Rect r,float arm,float lw,Color col)
        {
            BkFill(s,new Rect(r.x,r.y,arm,lw),col);BkFill(s,new Rect(r.x,r.y,lw,arm),col);BkFill(s,new Rect(r.xMax-arm,r.y,arm,lw),col);BkFill(s,new Rect(r.xMax-lw,r.y,lw,arm),col);
            BkFill(s,new Rect(r.x,r.yMax-lw,arm,lw),col);BkFill(s,new Rect(r.x,r.yMax-arm,lw,arm),col);BkFill(s,new Rect(r.xMax-arm,r.yMax-lw,arm,lw),col);BkFill(s,new Rect(r.xMax-lw,r.yMax-arm,lw,arm),col);
        }
        // Crown Halo: a nimbus behind the head, a fan of breathing light rays over it, a neon halo floating above and a few
        // glints twinkling round it.
        private static void BkCrownHalo(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t,float head)
        {
            var h=new Vector2(c.x,c.y-head*R);if(h.y<s.y+R*.2f)h.y=s.y+R*.2f;Color hot=BkHot(accent,.45f);
            BkGlow(s,c,R*1.6f,R*1.4f,U.A(accent,.1f*e),BkSoftAt,BkSoftA);BkGlow(s,h,R*1.05f,R*.95f,U.A(BkHot(accent,.25f),.26f*e),BkCoreAt,BkCoreA);
            int spx=BkPx(R*.7f,64);float r0=R*.42f;
            for(int k=0;k<NativePortraitBacks.HaloRays;k++)
            {
                int d=NativePortraitBacks.HaloRayDirection(k);float a=d*(360f/NativePortraitBacks.StreakSteps)*Mathf.Deg2Rad,dx=Mathf.Sin(a),dy=-Mathf.Cos(a);
                // Each ray ends inside the stage along its direction (a short ray rather than one cut at the edge).
                float reach=Mathf.Min(dx>1e-4f?(s.xMax-h.x)/dx:dx<-1e-4f?(s.x-h.x)/dx:1e6f,dy>1e-4f?(s.yMax-h.y)/dy:dy<-1e-4f?(s.y-h.y)/dy:1e6f)*.96f;
                NativePortraitBacks.HaloRay(k,t,out float length,out float alpha);float len=Mathf.Min(R*.7f*length,reach-r0);if(len<R*.08f)continue;float side=len/.94f,mid=r0+len/2;var p=new Vector2(h.x+dx*mid,h.y+dy*mid);
                BkImage(s,new Rect(p.x-side/2,p.y-side/2,side,side),BackTexture(BkStreak,d%NativePortraitBacks.StreakAngles,spx),U.A(hot,.72f*alpha*e));
            }
            // The halo floats above the head, low enough that its line and glow stay inside the stage (its half-height is .28 hw).
            float hw=R*.42f,size=hw*2/.9f,lineUnits=BkLine(R)*1.5f,hy=Mathf.Max(h.y-R*.5f,s.y+hw*.28f+lineUnits*3.8f+2);int px=BkPx(size);float line=lineUnits*px/size;
            var halo=BackTexture(BkEllipse,BkEllipseVariant(.28f,0),px,line,line*2.6f);var r=new Rect(h.x-size/2,hy-size/2,size,size);
            BkImage(s,r,halo,U.A(U.Mix(accent,Color.white,.25f),.9f*e));BkImage(s,r,BackTexture(BkEllipse,BkEllipseVariant(.28f,0),px,line*.55f,0),U.A(Color.white,.55f*e));
            float g=Mathf.Clamp(R*.07f,2.5f,10f),lw=Mathf.Max(1,g*.16f);
            for(int i=0;i<5;i++)
            {
                float tw=NativePortraitBacks.Twinkle(i,t);if(tw<.03f)continue;float ang=(-75+150*NativePortraitBacks.Hash(i,91))*Mathf.Deg2Rad,dist=R*(.62f+.38f*NativePortraitBacks.Hash(i,93));
                var p=new Vector2(h.x+Mathf.Sin(ang)*dist,h.y-Mathf.Cos(ang)*dist*.85f);float sz=g*(.6f+.4f*tw);
                BkFill(s,new Rect(p.x-sz,p.y-lw/2,sz*2,lw),U.A(hot,.85f*tw*e));BkFill(s,new Rect(p.x-lw/2,p.y-sz,lw,sz*2),U.A(hot,.85f*tw*e));BkDot(s,p,g*.9f,U.A(accent,.5f*tw*e),U.A(Color.white,tw*e));
            }
        }
        // Neon Diamond: a neon diamond frame (a white-hot core over its accent glow) with a fainter inner diamond, pulsing
        // corner nodes and two sparks racing round it in opposite directions.
        private static void BkNeonDiamond(Rect s,Vector2 c,float R,Color accent,Color body,float e,float t)
        {
            float lw=BkLine(R)*1.3f,D=Mathf.Min(R*1.28f,Mathf.Min(BkRoomX(s,c),BkRoomY(s,c))-lw*4-2);if(D<R*.4f)return;
            BkGlow(s,c,R*1.5f,R*1.35f,U.A(accent,.12f*e),BkSoftAt,BkSoftA);
            float size=D*2/.9f;int px=BkPx(size);float line=lw*px/size;var r=new Rect(c.x-size/2,c.y-size/2,size,size);
            BkImage(s,r,BackTexture(BkDiamond,0,px,line,line*3.6f),U.A(accent,.8f*e));BkImage(s,r,BackTexture(BkDiamond,1,px,line*.5f,0),U.A(BkHot(accent,.7f),.6f*e));
            float di=D*.78f,isize=di*2/.9f;int ipx=BkPx(isize);BkImage(s,new Rect(c.x-isize/2,c.y-isize/2,isize,isize),BackTexture(BkDiamond,0,ipx,lw*.7f*ipx/isize,lw*2.5f*ipx/isize),U.A(U.Mix(accent,body,.3f),.3f*e));
            Color hot=BkHot(accent,.6f);float node=Mathf.Clamp(R*.06f,2f,8f);
            for(int i=0;i<4;i++){NativePortraitBacks.DiamondPoint(i*.25f,out float x,out float y);float pulse=.55f+.45f*Mathf.Sin(t*2.2f+i*1.57f);BkDot(s,new Vector2(c.x+x*D,c.y+y*D),node*(1.6f+.4f*pulse),U.A(accent,.5f*pulse*e),U.A(hot,.9f*e));}
            for(int k=0;k<2;k++)
            {
                float at=NativePortraitBacks.DiamondSpark(k,t),dir=k%2==0?1:-1;
                for(int tail=5;tail>=0;tail--)
                {
                    NativePortraitBacks.DiamondPoint(at-dir*tail*.011f,out float x,out float y);float fade=tail==0?1:.55f-.09f*tail;
                    BkDot(s,new Vector2(c.x+x*D,c.y+y*D),node*(tail==0?1.9f:1.3f),U.A(accent,.55f*fade*e),U.A(Color.white,fade*e));
                }
            }
        }

        // ---- Locker card ---------------------------------------------------------------------------------------------------
        // A BACKDROP card's visual: the style live behind a voxel bust in the pilot's colours, clipped to the card's well. The
        // style takes `backdrop` (the picked backdrop colour, else the accent: NativeHatLooks.Back); the bust keeps the accent.
        private void LkBackVisual(Rect v,int style,float e,float hover,Color body,Color accent,float t,Color backdrop)
        {
            U.BeginBand(v);float R=Mathf.Min(v.height*.38f,v.width*.24f);var c=new Vector2(v.center.x,v.y+v.height*.52f);
            if(style==NativePortraitBacks.TickRing)PortraitRing(c,R,backdrop,e,t,20,true);else PortraitBack(style,v,c,R,backdrop,body,e,t,prefs.reducedEffects,.5f);
            // The bust, in the voxel pilot's proportions (PilotStage's pilot stands here): a big head with its accent visor,
            // the neck, and the torso with accent shoulder pads and the chest stripe, a little shaded so the backdrop reads.
            float u=R,lift=2*hover;var head=new Rect(c.x-u*.36f,c.y-u*.98f-lift,u*.72f,u*.66f);var torso=new Rect(c.x-u*.5f,c.y-u*.18f-lift,u*1f,v.yMax-c.y+u*.18f+lift+2);
            Color shade=BkShade(body,.86f);U.Fill(new Rect(c.x-u*.11f,head.yMax-1,u*.22f,torso.y-head.yMax+2),U.A(BkShade(body,.5f),e));
            U.Round(torso,U.A(shade,e),4);U.Fill(new Rect(torso.x,torso.y,u*.24f,u*.17f),U.A(accent,e));U.Fill(new Rect(torso.xMax-u*.24f,torso.y,u*.24f,u*.17f),U.A(accent,e));
            U.Fill(new Rect(c.x-u*.07f,torso.y+u*.12f,u*.14f,u*.42f),U.A(accent,.9f*e));U.Fill(new Rect(torso.x+u*.06f,torso.y+u*.62f,torso.width-u*.12f,u*.13f),U.A(BkShade(body,.35f),e));
            U.Round(head,U.A(body,e),4);U.Fill(new Rect(head.x+head.width*.12f,head.y+head.height*.42f,head.width*.76f,head.height*.18f),U.A(BkShade(accent,.9f),e));
            U.EndBand();
        }
    }
}
