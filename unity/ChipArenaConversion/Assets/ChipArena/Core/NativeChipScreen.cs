using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The wrist screen's place in the world (Unity axes): its centre, the axes a viewer in front of it reads it by (Right, Up)
    /// and its Normal toward that viewer, and its size (m).</summary>
    public struct ChipScreenPlace
    {
        public Vector3 Centre,Right,Up,Normal;public float Width,Height;
        /// <summary>A point on the screen, `x` m right and `y` m up of its centre.</summary>
        public Vector3 At(float x,float y)=>Centre+Right*x+Up*y;
    }

    /// <summary>One wrist screen's look this frame (everything NativeChipScreen.Build animates from).</summary>
    public struct ChipScreenLook
    {
        /// <summary>0 folded into the wrist .. 1 open (the look's unfold, folded again by a plant: NativeChipPlant.Fold).</summary>
        public float Unfold;
        /// <summary>The presentation clock (the motes' travel).</summary>
        public float Time;
        /// <summary>The cards (1-3), the lit one (hover or keyboard focus; -1 none), the card height (m; the faces' height, else the
        /// standard), the seed.</summary>
        public int Count,Lit,Seed;public float CardHeight;
        /// <summary>Mine: my offers' rarity colours, under the card faces the HUD draws over them. Anyone else's: blank cyan backs (their
        /// offers are private), the standard three. Alert: a damaging hit on this pilot just now (NativeChipScreen.Alert of its age), 0..1,
        /// shown on the screen's edge only.</summary>
        public bool Mine,Reduced,Low;public float Alert;public int C0,C1,C2;
        /// <summary>A pick (the wrist plant): its age, the card chosen, the others' fade (NativeChipPlant.Fade), and where and how big
        /// the plant's chip is (the hand takes the card there at NativeDraftPlant.Contact). Mine flies on the HUD (FlyCard false).</summary>
        public bool Plant,FlyCard;public int Chosen;public float PlantAge,Fade,CatchSize;public Vector3 Catch;
        /// <summary>Mine with the HUD's faces over it: the card slots a dim frame just outside each face (an affine face cannot follow
        /// the row's perspective exactly, so the residual reads as the slot's glow).</summary>
        public bool Faced;
    }

    /// <summary>The wrist screen's vertex stream (Runtime/NativeChipScreenPresentation.cs fills a mesh with five UV channels;
    /// Resources/NativeChipScreen.shader reads them; NativeChipScreen.Build documents each channel).</summary>
    public interface INativeChipScreenSink
    {
        int Vertex(Vector3 at,Vector4 a,Vector4 b,Vector4 c,Vector4 d,Vector4 e);
        void Triangle(int a,int b,int c);
    }

    /// <summary>
    /// The screen's light budget (owner 2026-10-15: "Holographic display kind of buggy, and display is odd. Can we make it like a better
    /// holographic display with no artifacting?"), in linear HDR as the match camera renders it (Linear colour space, an ARGBHalf target,
    /// then NativeBloom: threshold 1 on the brightest channel). Every value is the light a part adds at its brightest, as a share of its
    /// tint's brightest channel (tints are sRGB colours, at most 1 in linear). Resources/NativeChipScreen.shader's SHARED block defines the
    /// same numbers (CS_*); Tests/ChipScreenChecks reads them from the shader and sums them.
    ///  * The screen is one alpha-blended quad (premultiplied: Blend One OneMinusSrcAlpha, ZWrite Off): dark glass that hides BodyAlpha of
    ///    what is behind it and adds a little light of its own. Drawing it again converges on its own colour instead of adding up.
    ///  * Its body (glass, grid, scanlines, the soft inner rim, the fresnel sheen, the card slots and a blank back's emblem, the rays and
    ///    motes seen through it) sums to BodyBudget, under the bloom's threshold even over a background at the threshold.
    ///  * Only the thin frame line, its corner brackets, the emitter ring and a lit card's rim go over it: a soft glow of a few pixels.
    /// </summary>
    public static class NativeChipScreenLight
    {
        /// <summary>NativeBloomRules.Threshold: linear light at or under it never blooms.</summary>
        public const float Threshold=NativeBloomRules.Threshold;
        /// <summary>The glass (its alpha and light), the grid, the scanlines, the soft inner rim (and how far in it fades, m), the fresnel
        /// sheen at a glancing view.</summary>
        public const float BodyAlpha=.58f,BodyLight=.05f,GridLight=.025f,ScanLight=.02f,RimLight=.16f,RimWidth=.022f,FresnelLight=.06f;
        /// <summary>A card slot (it darkens the glass further), a blank back's emblem, and under the HUD's faces the slot's glow.</summary>
        public const float SlotAlpha=.35f,SlotLight=.02f,GlyphLight=.14f,FacedGlow=.05f;
        /// <summary>The lines: the header and footer rules, the frame line (its width, m) and its corner brackets, a card's rim (lit), the
        /// frame just outside a face (lit), the scan line that reveals the cards as it opens.</summary>
        public const float RuleLight=.35f,LineLight=1.45f,LineWidth=.0022f,BracketLight=1.2f,CardRimLight=.55f,CardLitLight=1.05f,FacedRimLight=.32f,FacedLitLight=.6f,ScanInLight=1f;
        /// <summary>Behind the screen: the rays from the wrist (their light at the wrist end, and their alpha), the emitter ring, the motes.</summary>
        public const float RayLight=.28f,RayAlpha=.12f,RingLight=1.2f,MoteLight=.3f;
        /// <summary>Everything that can light one pixel of the screen's body, each at its brightest (a blank back's emblem or a face's glow,
        /// never both), with what the glass lets through of the rays and motes behind it.</summary>
        public const float BodyBudget=BodyLight+GridLight+ScanLight+RimLight+FresnelLight+SlotLight+(GlyphLight>FacedGlow?GlyphLight:FacedGlow)+(RayLight+MoteLight)*(1-BodyAlpha);
        /// <summary>A bright lit wall behind the screen (linear; the hub's brightest walls are under it), seen through the glass.</summary>
        public const float BrightScene=.5f;
    }

    /// <summary>
    /// The Expedition chip screen (owner 2026-10-15: "we should also make it a holographic screen projected from the wrist. That way its
    /// easier to notice were selecting a chip"; rebuilt the same day for "Holographic display kind of buggy, and display is odd. Can we make
    /// it like a better holographic display with no artifacting?"): the chooser's display wrist projects a hardlight screen that floats in
    /// front of the pilot, on the display wrist's side at about chest height, turned toward the other front quarter (where the chooser camera
    /// stands: NativeChooserCamera).
    ///  * One quad (Resources/NativeChipScreen.shader): dark glass with a faint grid and scanlines, a soft inner rim and fresnel sheen, a
    ///    thin bright chamfered frame line with corner brackets, header and footer rules, and the card slots inside it (no stacked or
    ///    coplanar layers). Thin rays from the wrist emitter to its corners, an emitter ring and a few motes, all behind it.
    ///  * Opening, it unfolds out of the wrist (its centre travels out of the emitter, its width first, then its height) with a short
    ///    flicker, and a scan line reveals the cards; closing, it folds back the same way. No flicker once open.
    ///  * Mine carries my offers' rarities in the slots under the real card faces the HUD draws over it (Runtime/NativeExpeditionChipScreen.cs
    ///    maps its canvas onto the projected screen: Map); anyone else's shows the standard three as blank cyan backs, never an offer.
    ///  * A damaging hit turns its frame line and rim red for a moment (Alert), never its body.
    ///  * A pick: the chosen card leaves its place and flies into the hand at the plant's contact (.62 s), shrinking to the chip; the
    ///    others fade (NativeChipPlant.Fade) and the screen folds into the wrist (NativeChipPlant.Fold), opening again for a queued pick.
    /// Light: NativeChipScreenLight. Presentation only, engine-free and allocation-free.
    /// </summary>
    public static class NativeChipScreen
    {
        /// <summary>The screen over the body centre (Up), ahead of it (Ahead) and out to the display wrist's side (Aside), turned Turn
        /// degrees from straight ahead toward the other side; its size (m, by rig scale).</summary>
        public const float Up=.36f,Ahead=.74f,Aside=.42f,Turn=30,Width=1.06f,Height=.52f;
        /// <summary>The cards: width, the standard height, the gap between them and the row's centre under the screen's centre (m).</summary>
        public const float CardWidth=.3f,CardHeight=.19f,CardGap=.026f,CardsY=-.012f;
        /// <summary>The HUD canvas units per metre of screen (one unit a millimetre).</summary>
        public const float Canvas=1000;
        /// <summary>Unfold: the width opens over the first part, the height over the rest (Electron's grow from a line).</summary>
        public const float WidthFirst=.55f;

        /// <summary>The side the screen floats on: the side the display wrist is held on, so the beam does not cross the chest. That is
        /// the right wrist's (NativeChipSelect.DisplayLeft: the right for every class since 2026-10-22; the rifles and the greatsword
        /// hold it low on that side), except the shield set, whose stance turns the chest so far that the raised right wrist sits left
        /// of the body's middle.</summary>
        public static bool OnLeft(string spec)=>spec=="shield"||NativeChipSelect.DisplayLeft(spec)&&spec!="rifle"&&spec!="machinegun"&&spec!="greatsword";
        /// <summary>The open screen of a pilot whose body centre is `body`, facing `facing` (Unity yaw), on the left or the right (OnLeft).</summary>
        public static ChipScreenPlace Place(Vector3 body,float facing,bool left,float scale)
        {
            float s=scale>0?scale:1,side=left?-1:1;var f=Flat(facing);var r=new Vector3(f.Z,0,-f.X);
            var n=Flat(facing-side*Turn);var right=new Vector3(-n.Z,0,n.X);
            return new ChipScreenPlace{Centre=body+Vector3.UnitY*(Up*s)+f*(Ahead*s)+r*(side*Aside*s),Right=right,Up=Vector3.UnitY,Normal=n,Width=Width*s,Height=Height*s};
        }
        private static Vector3 Flat(float yaw){double y=yaw*Math.PI/180;return new Vector3((float)Math.Sin(y),0,(float)Math.Cos(y));}

        /// <summary>The screen as drawn at `unfold`: out of the emitter, its width opening first, then its height.</summary>
        public static ChipScreenPlace Shown(in ChipScreenPlace open,Vector3 emitter,float unfold)
        {
            float k=MathUtil.Clamp(unfold,0,1),w=MathUtil.Smooth(k/WidthFirst),h=MathUtil.Smooth((k-WidthFirst*.6f)/(1-WidthFirst*.6f)),travel=MathUtil.Smooth(k/.8f);
            var p=open;p.Centre=Vector3.Lerp(emitter,open.Centre,travel);p.Width=open.Width*(.04f+.96f*w);p.Height=open.Height*(.02f+.98f*h);return p;
        }

        /// <summary>Card `i` of `count`: its centre (m from the screen's centre: x right, y up) on the open screen.</summary>
        public static Vector2 Slot(int i,int count,float width=Width)
        {
            count=Math.Max(1,Math.Min(3,count));float scale=width/Width,span=count*CardWidth+(count-1)*CardGap;
            return new Vector2((-span*.5f+CardWidth*.5f+i*(CardWidth+CardGap))*scale,CardsY*scale);
        }

        /// <summary>
        /// The HUD's map of (a band of) the screen: canvas point (u right, v down; Canvas units a metre from the open screen's top-left
        /// corner) to the screen point the camera shows it at. Affine, as the HUD draws it, so it is fitted per band (the header, the cards,
        /// the footer) where the camera projects that band's corners, close to the true perspective there. Inverse maps a cursor back onto
        /// the canvas (false when the projection is degenerate).
        /// </summary>
        public struct CanvasMap
        {
            public Vector2 Origin,AxisU,AxisV;
            /// <summary>The map through three canvas points and where they show: `c1` along the row of `c0`, `c2` down its column.</summary>
            public static CanvasMap Fit(Vector2 c0,Vector2 s0,Vector2 c1,Vector2 s1,Vector2 c2,Vector2 s2)
            {
                var u=(s1-s0)/Math.Max(1e-4f,c1.X-c0.X);var v=(s2-s0)/Math.Max(1e-4f,c2.Y-c0.Y);return new CanvasMap{Origin=s0-u*c0.X-v*c0.Y,AxisU=u,AxisV=v};
            }
            /// <summary>The map of the canvas rect (u0, v0)-(u1, v1) from where its four corners show: the mean of its edges, through
            /// the mean of its corners (half the far corner's error of a three-point fit under perspective).</summary>
            public static CanvasMap Rect(float u0,float v0,float u1,float v1,Vector2 topLeft,Vector2 topRight,Vector2 bottomLeft,Vector2 bottomRight)
            {
                var u=(topRight-topLeft+bottomRight-bottomLeft)/(2*Math.Max(1e-4f,u1-u0));var v=(bottomLeft-topLeft+bottomRight-topRight)/(2*Math.Max(1e-4f,v1-v0));
                var centre=(topLeft+topRight+bottomLeft+bottomRight)*.25f;return new CanvasMap{Origin=centre-u*((u0+u1)*.5f)-v*((v0+v1)*.5f),AxisU=u,AxisV=v};
            }
            /// <summary>The whole screen's map from where its top-left, top-right and bottom-left corners show.</summary>
            public static CanvasMap From(Vector2 topLeft,Vector2 topRight,Vector2 bottomLeft,float width,float height)
                =>Fit(Vector2.Zero,topLeft,new Vector2(width*Canvas,0),topRight,new Vector2(0,height*Canvas),bottomLeft);
            public Vector2 Screen(Vector2 canvas)=>Origin+AxisU*canvas.X+AxisV*canvas.Y;
            public bool Inverse(Vector2 screen,out Vector2 canvas)
            {
                float det=AxisU.X*AxisV.Y-AxisU.Y*AxisV.X;canvas=default;if(Math.Abs(det)<1e-9f||!MathUtil.Finite(det))return false;
                var d=screen-Origin;canvas=new Vector2((d.X*AxisV.Y-d.Y*AxisV.X)/det,(AxisU.X*d.Y-AxisU.Y*d.X)/det);return true;
            }
            /// <summary>Canvas units to screen units along the screen's width (its on-screen scale).</summary>
            public float Scale=>AxisU.Length();
        }
        /// <summary>The world point of canvas point (u, v) on a screen as drawn (the canvas always spans it, whatever its unfold or scale).</summary>
        public static Vector3 CanvasPoint(in ChipScreenPlace p,float u,float v)=>p.At((u/(Width*Canvas)-.5f)*p.Width,(.5f-v/(Height*Canvas))*p.Height);
        /// <summary>Card `i`'s rect on the canvas (left, top, width, height), for `count` cards of `cardHeight` m.</summary>
        public static Vector4 CardRect(int i,int count,float cardHeight=CardHeight)
        {
            var c=Slot(i,count);float h=cardHeight>0?cardHeight:CardHeight;
            return new Vector4((c.X-CardWidth*.5f+Width*.5f)*Canvas,(Height*.5f-c.Y-h*.5f)*Canvas,CardWidth*Canvas,h*Canvas);
        }
        /// <summary>The card a cursor (screen point) is over, through the screen's map: -1 when it is on none.</summary>
        public static int Hit(in CanvasMap map,Vector2 cursor,int count,float cardHeight=CardHeight)
        {
            if(!map.Inverse(cursor,out var c))return -1;count=Math.Max(1,Math.Min(3,count));
            for(int i=0;i<count;i++){var r=CardRect(i,count,cardHeight);if(c.X>=r.X&&c.X<=r.X+r.Z&&c.Y>=r.Y&&c.Y<=r.Y+r.W)return i;}
            return -1;
        }

        /// <summary>Where the chosen card is `age` s into a pick: from its place on the open screen to the hand at the contact, the
        /// path bowing toward the viewer; its width from the card's to the chip's; gone once the hand has it.</summary>
        public static bool Fly(in ChipScreenPlace open,int chosen,int count,float age,Vector3 catchAt,float catchSize,out Vector3 at,out float width)
        {
            at=default;width=0;if(!(age>=0)||age>=NativeDraftPlant.Contact)return false;
            float t=MathUtil.Smooth(age/NativeDraftPlant.Contact);var slot=Slot(chosen,count,open.Width);var from=open.At(slot.X,slot.Y)+open.Normal*.02f;
            at=Vector3.Lerp(from,catchAt,t)+open.Normal*(.12f*(float)Math.Sin(Math.PI*t))+Vector3.UnitY*(.05f*(float)Math.Sin(Math.PI*t));
            width=CardWidth*(open.Width/Width)+(Math.Max(.005f,catchSize)-CardWidth*(open.Width/Width))*t;return true;
        }

        /// <summary>The vertex stream's kinds (TEXCOORD0.z).</summary>
        public const float KindScreen=0,KindRay=1,KindRing=2,KindMote=3,KindCard=4;
        /// <summary>The panel's card modes (TEXCOORD2.x = count + 4 x mode).</summary>
        public const int ModeBacks=0,ModeFaced=1,ModeMine=2;
        /// <summary>Behind the screen: the motes (none with Reduced effects) and how far along a ray they travel (they never reach the
        /// screen's plane); the rays' width at the wrist and at the corner, and the screen's height from which a ray goes to each corner (m, by
        /// rig scale).</summary>
        public const int Motes=6;public const float MoteReach=.78f,RayNear=.0018f,RayFar=.0034f,RaysApart=.12f;
        /// <summary>The red edge after a damaging hit: full at the hit, gone AlertSpan s later (within the hit search's window,
        /// NativeChipSelect.FlinchRise + FlinchFall).</summary>
        public const float AlertSpan=.45f;
        /// <summary>The alert for a hit `age` s ago (+inf, NaN or negative: none).</summary>
        public static float Alert(float age)=>!(age>=0)||age>=AlertSpan?0:1-MathUtil.Smooth(age/AlertSpan);
        /// <summary>A colour for the vertex stream: its sRGB hex, exact in a float.</summary>
        public static float Pack(int hex)=>hex&0xffffff;
        /// <summary>The opening's flicker: a few quick dips while it unfolds (none with Reduced effects), steady once open.</summary>
        public static float Boot(float unfold,int seed,bool reduced)=>reduced||unfold>=.999f?1:.72f+.28f*(float)Math.Abs(Math.Sin(unfold*19+(seed&1023)*.007));
        /// <summary>The scan that reveals the cards as the height opens (1 once open; no scan with Reduced effects).</summary>
        public static float Scan(float unfold,bool reduced)=>reduced?1:MathUtil.Smooth((MathUtil.Clamp(unfold,0,1)-.6f)/.4f);

        private static void Quad(INativeChipScreenSink s,Vector3 bl,Vector3 br,Vector3 tr,Vector3 tl,float kind,float opacity,Vector4 b,Vector4 c,Vector4 d,Vector4 e)
        {
            int i0=s.Vertex(bl,new Vector4(0,0,kind,opacity),b,c,d,e),i1=s.Vertex(br,new Vector4(1,0,kind,opacity),b,c,d,e);
            int i2=s.Vertex(tr,new Vector4(1,1,kind,opacity),b,c,d,e),i3=s.Vertex(tl,new Vector4(0,1,kind,opacity),b,c,d,e);
            s.Triangle(i0,i1,i2);s.Triangle(i0,i2,i3);
        }
        /// <summary>The quads a look builds at most (the tests' bound; Build returns the count it wrote).</summary>
        public static int Quads(in ChipScreenLook look)
        {
            float unfold=MathUtil.Clamp(look.Unfold,0,1);int n=0;
            if(unfold>.002f){n++;if(!look.Low)n+=4+1+(look.Reduced?0:Motes);}// the screen; the rays, the ring, the motes
            if(look.Plant&&look.FlyCard&&look.PlantAge>=0&&look.PlantAge<NativeDraftPlant.Contact)n++;
            return n;
        }

        /// <summary>
        /// Builds one wrist screen into `s` (world space), back to front: the rays from `emitter` to its corners, the emitter ring and the
        /// motes (behind it), the screen, and a pick's flying card (in front of it). Returns the quads written (0 when folded away). The
        /// stream (Resources/NativeChipScreen.shader):
        ///  TEXCOORD0 xy the quad's uv (0..1, y up; a ray's y from the wrist), z the kind, w its opacity;
        ///  TEXCOORD1 the screen: its width and height (m) as drawn, the alert, the cards' scan; a ray: its width (m) at the corner end;
        ///  TEXCOORD2 the screen: count + 4 x mode, the card height (a share of the screen's), the lit card (-1 none), the seed;
        ///            the rest: x the tint (Pack);
        ///  TEXCOORD3 the screen: each card's visibility (dealt x faded; 0 for the chosen card of a pick), w unused;
        ///  TEXCOORD4 the screen: the cards' tints and the screen's tone (Pack).
        /// </summary>
        public static int Build(INativeChipScreenSink s,in ChipScreenPlace open,Vector3 emitter,in ChipScreenLook look)
        {
            float unfold=MathUtil.Clamp(MathUtil.Finite(look.Unfold)?look.Unfold:0,0,1);int quads=0,count=Math.Max(1,Math.Min(3,look.Count));
            float seed=(look.Seed&1023)/1024f,alert=MathUtil.Clamp(MathUtil.Finite(look.Alert)?look.Alert:0,0,1);
            float tone=Pack(NativeChipSelectHolo.Cyan),soft=Pack(NativeChipSelectHolo.CyanSoft);
            if(unfold>.002f)
            {
                var p=Shown(open,emitter,unfold);var r=p.Right*(p.Width*.5f);var u=p.Up*(p.Height*.5f);
                float opacity=MathUtil.Clamp(unfold*1.6f,0,1)*Boot(unfold,look.Seed,look.Reduced),scale=open.Width/Width;
                var bl=p.Centre-r-u;var br=p.Centre+r-u;var tr=p.Centre+r+u;var tl=p.Centre-r+u;
                if(!look.Low)
                {
                    // The rays: thin ribbons from the wrist to the four corners, turned to face along the screen's normal (toward its viewer);
                    // while the screen is still a sliver, two, to the middles of its ends (four would lie on one another).
                    var none=Vector4.Zero;var tint=new Vector4(soft,0,0,0);
                    if(p.Height>=RaysApart*scale)quads+=Ray(s,emitter,bl,p.Normal,opacity,tint,scale)+Ray(s,emitter,br,p.Normal,opacity,tint,scale)+Ray(s,emitter,tr,p.Normal,opacity,tint,scale)+Ray(s,emitter,tl,p.Normal,opacity,tint,scale);
                    else quads+=Ray(s,emitter,p.Centre-r,p.Normal,opacity,tint,scale)+Ray(s,emitter,p.Centre+r,p.Normal,opacity,tint,scale);
                    var rr=p.Right*(.026f*scale);var ru=p.Up*(.026f*scale);var e0=emitter-p.Normal*.002f;
                    Quad(s,e0-rr-ru,e0+rr-ru,e0+rr+ru,e0-rr+ru,KindRing,opacity,none,tint,none,none);quads++;
                    if(!look.Reduced)for(int i=0;i<Motes;i++)
                    {
                        // Each travels out along a ray (two to the bottom corners, one to each other corner and so on), short of the screen.
                        float k=((look.Time/1.1f+i/(float)Motes+seed)%1+1)%1;var corner=(i%4)==0?bl:(i%4)==1?br:(i%4)==2?tr:tl;
                        var at=Vector3.Lerp(emitter,corner,k*MoteReach);float a=(float)Math.Sin(Math.PI*k),m=.007f*scale;
                        Quad(s,at-p.Right*m-p.Up*m,at+p.Right*m-p.Up*m,at+p.Right*m+p.Up*m,at-p.Right*m+p.Up*m,KindMote,opacity*a,none,tint,none,none);quads++;
                    }
                }
                // The screen: one quad; its cards are drawn inside it (the shader), dealt as it opens. A pick: the chosen card has left (below)
                // and the others fade.
                float fade=look.Plant?MathUtil.Clamp(look.Fade,0,1):1;int chosen=look.Plant?Chosen(look,count):-1;
                float V(int i)=>i>=count?0:(i==chosen?0:fade)*NativeChipSelectHolo.Deal(unfold,float.PositiveInfinity,i);
                int mode=!look.Mine?ModeBacks:look.Faced?ModeFaced:ModeMine;
                int c0=look.Mine?look.C0:NativeChipSelectHolo.Cyan,c1=look.Mine?look.C1:NativeChipSelectHolo.Cyan,c2=look.Mine?look.C2:NativeChipSelectHolo.Cyan;
                float card=(look.CardHeight>0?look.CardHeight:CardHeight)/Height;
                Quad(s,bl,br,tr,tl,KindScreen,opacity,new Vector4(p.Width,p.Height,alert,Scan(unfold,look.Reduced)),new Vector4(count+4*mode,card,look.Mine?look.Lit:-1,seed),
                    new Vector4(V(0),V(1),V(2),0),new Vector4(Pack(c0),Pack(c1),Pack(c2),tone));quads++;
            }
            // A pick's chosen card on its way to the hand (anyone else's, or mine without the HUD's faces), in front of the screen.
            if(look.Plant&&look.FlyCard&&Fly(open,Chosen(look,count),count,look.PlantAge,look.Catch,look.CatchSize,out var flyAt,out float flyWidth))
            {
                int rgb=look.Mine?(Chosen(look,count)==0?look.C0:Chosen(look,count)==1?look.C1:look.C2):NativeChipSelectHolo.Cyan;float hw=flyWidth*.5f,hh=hw*((look.CardHeight>0?look.CardHeight:CardHeight)/CardWidth);
                var r=open.Right*hw;var u=open.Up*hh;var z=Vector4.Zero;
                Quad(s,flyAt-r-u,flyAt+r-u,flyAt+r+u,flyAt-r+u,KindCard,1,new Vector4(flyWidth,hh*2,0,0),new Vector4(Pack(rgb),0,0,0),z,z);quads++;
            }
            return quads;
        }
        // A ray: a ribbon from the emitter ring's edge (y 0) to a corner (y 1), across the plane of the ray and the screen's normal (so the
        // rays leave the ring apart and never overlap one another).
        private static int Ray(INativeChipScreenSink s,Vector3 emitter,Vector3 to,Vector3 normal,float opacity,Vector4 tint,float scale)
        {
            var d0=to-emitter;float len=d0.Length();if(!(len>=1e-4f))return 0;var from=emitter+d0*(Math.Min(.026f*scale,len*.5f)/len);var d=to-from;var side=Vector3.Cross(d,normal);if(side.LengthSquared()<1e-10f)side=Vector3.Cross(d,Vector3.UnitY);if(side.LengthSquared()<1e-10f)side=Vector3.UnitX;side=Vector3.Normalize(side);
            var a=side*(RayNear*.5f*scale);var b=side*(RayFar*.5f*scale);var z=Vector4.Zero;
            Quad(s,from-a,from+a,to+b,to-b,KindRay,opacity,new Vector4(RayFar*scale,0,0,0),tint,z,z);return 1;
        }
        private static int Chosen(in ChipScreenLook l,int count)=>l.Chosen>=0?l.Chosen%count:count/2;
    }
}
