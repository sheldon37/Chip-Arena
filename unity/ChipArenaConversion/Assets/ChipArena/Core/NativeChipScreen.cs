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
        /// <summary>The presentation clock (flicker, scan, motes).</summary>
        public float Time;
        /// <summary>The cards (1-3), the lit one (hover or keyboard focus; -1 none), the card height (m; the faces' height, else the
        /// standard), the seed.</summary>
        public int Count,Lit,Seed;public float CardHeight;
        /// <summary>Mine: my offers' rarity colours, under the card faces the HUD draws over them. Anyone else's: blank cyan backs (their
        /// offers are private), the standard three.</summary>
        public bool Mine,Attacked,Reduced,Low;public int C0,C1,C2;
        /// <summary>A pick (the wrist plant): its age, the card chosen, the others' fade (NativeChipPlant.Fade), and where and how big
        /// the plant's chip is (the hand takes the card there at NativeDraftPlant.Contact). Mine flies on the HUD (FlyCard false).</summary>
        public bool Plant,FlyCard;public int Chosen;public float PlantAge,Fade,CatchSize;public Vector3 Catch;
        /// <summary>Mine with the HUD's faces over it: the cards a dim backing glow (an affine face cannot follow the row's perspective
        /// exactly, so the residual reads as glow).</summary>
        public bool Faced;
    }

    /// <summary>
    /// The Expedition chip screen (owner 2026-10-15: "we should also make it a holographic screen projected from the wrist. That way its
    /// easier to notice were selecting a chip"): the chooser's display wrist projects a hardlight screen that floats in front of the pilot,
    /// on the display wrist's side at about chest height, turned toward the other front quarter (where the chooser camera stands:
    /// NativeChooserCamera). It is the wrist hologram's look grown into a screen: a beam fans from the wrist emitter into it, a chamfered
    /// frame with a header line, scanlines and flicker (Resources/NativeChipSelect.shader, the same kinds), the cards in a row.
    ///  - Opening, it unfolds out of the wrist (its centre travels out of the emitter, its width first, then its height) and deals the
    ///    cards; closing, it folds back into the wrist the same way.
    ///  - Mine carries my offers' rarities under the real card faces the HUD draws over it (Runtime/NativeExpeditionChipScreen.cs maps
    ///    its canvas onto the projected screen: Map); anyone else's shows the standard three as blank cyan backs, never an offer.
    ///  - A pick: the chosen card leaves its place and flies into the hand at the plant's contact (.62 s), shrinking to the chip; the
    ///    others fade (NativeChipPlant.Fade) and the screen folds into the wrist (NativeChipPlant.Fold), opening again for a queued pick.
    /// Presentation only, engine-free and allocation-free.
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

        /// <summary>The side the screen floats on: the display wrist's (NativeChipSelect.DisplayLeft), except where the class holds that
        /// wrist across the body in both hands (the rifle, the machine gun, the greatsword): there it floats on the side the wrist is
        /// held, so the beam does not cross the chest.</summary>
        public static bool OnLeft(string spec)=>NativeChipSelect.DisplayLeft(spec)&&spec!="rifle"&&spec!="machinegun"&&spec!="greatsword";
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

        private static Vector4 Col(int hex,float a)=>new Vector4(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f,a);
        private static void Quad(INativeChipHoloSink s,Vector3 c,Vector3 r,Vector3 u,Vector4 col,float kind,float param,float gain,float seed)
        {
            int a=s.Vertex(c-r-u,col,new Vector4(0,0,kind,param),new Vector4(gain,seed,0,0)),b=s.Vertex(c+r-u,col,new Vector4(1,0,kind,param),new Vector4(gain,seed,0,0));
            int d=s.Vertex(c+r+u,col,new Vector4(1,1,kind,param),new Vector4(gain,seed,0,0)),e=s.Vertex(c-r+u,col,new Vector4(0,1,kind,param),new Vector4(gain,seed,0,0));
            s.Triangle(a,b,d);s.Triangle(a,d,e);
        }
        private static void Sheet(INativeChipHoloSink s,Vector3 a0,Vector3 a1,Vector3 b1,Vector3 b0,Vector4 col,float kind,float param,float gain,float seed)
        {
            int a=s.Vertex(a0,col,new Vector4(0,0,kind,param),new Vector4(gain,seed,0,0)),b=s.Vertex(a1,col,new Vector4(1,0,kind,param),new Vector4(gain,seed,0,0));
            int d=s.Vertex(b1,col,new Vector4(1,1,kind,param),new Vector4(gain,seed,0,0)),e=s.Vertex(b0,col,new Vector4(0,1,kind,param),new Vector4(gain,seed,0,0));
            s.Triangle(a,b,d);s.Triangle(a,d,e);
        }
        public const int Motes=7;
        /// <summary>The quads a look builds (the tests' bound; Build returns the count it wrote).</summary>
        public static int Quads(in ChipScreenLook look)
        {
            float unfold=MathUtil.Clamp(look.Unfold,0,1);int n=0;
            if(unfold>.002f)
            {
                int count=Math.Max(1,Math.Min(3,look.Count));n+=2+count;// frame, header line, cards (each at most)
                if(!look.Low)n+=2+2+1+(look.Reduced?0:Motes);// beam sheets, beam edges, emitter ring, motes
            }
            if(look.Plant&&look.FlyCard&&look.PlantAge>=0&&look.PlantAge<NativeDraftPlant.Contact)n++;
            return n;
        }

        /// <summary>Builds one wrist screen into `s` (world space): the beam from `emitter`, the frame, the header line, the cards and a
        /// pick's flying card. Returns the quads written (0 when folded away).</summary>
        public static int Build(INativeChipHoloSink s,in ChipScreenPlace open,Vector3 emitter,in ChipScreenLook look)
        {
            float unfold=MathUtil.Clamp(MathUtil.Finite(look.Unfold)?look.Unfold:0,0,1);int quads=0,count=Math.Max(1,Math.Min(3,look.Count));
            bool red=NativeChipSelectHolo.Red(look.Attacked,look.Time,look.Reduced);float seed=(look.Seed&1023)/1024f;
            int tone=red?NativeChipSelectHolo.Danger:NativeChipSelectHolo.Cyan,soft=red?0xff9aa8:NativeChipSelectHolo.CyanSoft;
            // Flicker on open (a few hard blinks as it boots), then the hologram's steady flicker and glitch.
            float boot=look.Reduced?1:unfold>=.999f?1:.55f+.45f*(float)Math.Abs(Math.Sin(unfold*23+seed*7));
            float alpha=MathUtil.Clamp(unfold*1.6f,0,1)*NativeChipSelectHolo.Flicker(look.Time,look.Seed,look.Reduced)*boot;
            if(unfold>.002f)
            {
                var p=Shown(open,emitter,unfold);var r=p.Right*(p.Width*.5f);var u=p.Up*(p.Height*.5f);
                NativeChipScreenFrame(s,p,r,u,tone,soft,alpha,unfold,seed,ref quads);
                if(!look.Low)
                {
                    // The beam: a sheet from the emitter fanning to the screen's bottom edge, a fainter one to its upper part, its two edges.
                    var bl=p.Centre-r-u;var br=p.Centre+r-u;var tl=p.Centre-r+u*.4f;var tr=p.Centre+r+u*.4f;var e0=emitter-p.Right*.014f;var e1=emitter+p.Right*.014f;
                    Sheet(s,e0,e1,br,bl,Col(tone,.42f*alpha),NativeChipSelectHolo.KindBeam,0,1.15f,seed);quads++;
                    Sheet(s,e0,e1,tr,tl,Col(tone,.18f*alpha),NativeChipSelectHolo.KindBeam,1,1,seed);quads++;
                    var side=Vector3.Cross(p.Normal,p.Up)*.003f;
                    Sheet(s,emitter-side,emitter+side,bl+side,bl-side,Col(soft,.6f*alpha),NativeChipSelectHolo.KindLine,1,1.6f,seed);quads++;
                    Sheet(s,emitter-side,emitter+side,br+side,br-side,Col(soft,.6f*alpha),NativeChipSelectHolo.KindLine,1,1.6f,seed);quads++;
                    Quad(s,emitter+p.Normal*.002f,p.Right*.026f,p.Up*.026f,Col(soft,.9f*alpha),NativeChipSelectHolo.KindRing,.3f,2.4f,seed);quads++;
                    if(!look.Reduced)for(int i=0;i<Motes;i++)
                    {
                        float k=(look.Time/.8f+i/(float)Motes+seed)%1,x=(NativeChipSelect.Hash(look.Seed,i,13)*2-1)*.9f;var at=Vector3.Lerp(emitter,p.Centre-u+r*x,k);
                        Quad(s,at,p.Right*.008f,p.Up*.008f,Col(soft,alpha*(float)Math.Sin(Math.PI*k)),NativeChipSelectHolo.KindMote,0,2,seed);quads++;
                    }
                }
                // The cards, dealt as it opens: mine in my offers' rarities (the faces drawn over them), anyone else's blank cyan backs.
                // The lit card stands out toward the viewer, brighter. A pick: the chosen card has left (below); the others fade.
                float fade=look.Plant?MathUtil.Clamp(look.Fade,0,1):1,ch=(look.CardHeight>0?look.CardHeight:CardHeight)*(p.Height/Height)*.5f;
                for(int i=0;i<count;i++)
                {
                    if(look.Plant&&(i==Chosen(look,count)||fade<=.01f))continue;
                    float deal=NativeChipSelectHolo.Deal(unfold,float.PositiveInfinity,i);if(deal<=.01f)continue;bool lit=i==look.Lit;
                    var c=Slot(i,count,p.Width);var at=p.At(c.X,c.Y)+p.Normal*(.008f+(lit?.025f:0));
                    int rgb=red?NativeChipSelectHolo.Danger:look.Mine?(i==0?look.C0:i==1?look.C1:look.C2):NativeChipSelectHolo.Cyan;
                    float grow=(lit?1.05f:1)*(.7f+.3f*deal);
                    Quad(s,at,p.Right*(CardWidth*(p.Width/Width)*.5f*grow),p.Up*(ch*grow*deal),Col(rgb,alpha*(look.Faced?(lit?.5f:.38f):lit?1:look.Mine?.8f:.6f)*deal*fade),NativeChipSelectHolo.KindCard,lit?1:0,lit?1.6f:1.1f,seed+i*.13f);quads++;
                }
            }
            // A pick's chosen card on its way to the hand (anyone else's: mine flies on the HUD over the screen).
            if(look.Plant&&look.FlyCard&&Fly(open,Chosen(look,count),count,look.PlantAge,look.Catch,look.CatchSize,out var flyAt,out float flyWidth))
            {
                int rgb=look.Mine?(Chosen(look,count)==0?look.C0:Chosen(look,count)==1?look.C1:look.C2):NativeChipSelectHolo.Cyan;float half=flyWidth*.5f;
                Quad(s,flyAt,open.Right*half,open.Up*(half*((look.CardHeight>0?look.CardHeight:CardHeight)/CardWidth)),Col(rgb,1),NativeChipSelectHolo.KindCard,1,1.8f,seed+.5f);quads++;
            }
            return quads;
        }
        private static int Chosen(in ChipScreenLook l,int count)=>l.Chosen>=0?l.Chosen%count:count/2;
        // The frame (the shader's chamfered hardlight panel with its brackets and scanlines) and the header line along its top edge.
        private static void NativeChipScreenFrame(INativeChipHoloSink s,in ChipScreenPlace p,Vector3 r,Vector3 u,int tone,int soft,float alpha,float unfold,float seed,ref int quads)
        {
            Quad(s,p.Centre,r,u,Col(tone,alpha*.85f),NativeChipSelectHolo.KindFrame,unfold,1.5f,seed);quads++;
            Quad(s,p.Centre+p.Up*(p.Height*.5f+.012f),r*.92f,p.Up*.004f,Col(soft,alpha*(.6f+.4f*(1-unfold))),NativeChipSelectHolo.KindLine,0,2.2f,seed);quads++;
        }
    }
}
