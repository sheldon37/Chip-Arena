using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using Cart=ChipArena.UnityConversion.NativeChipCartridge;
namespace ChipArena.UnityConversion
{
    // The chip's 2D art, drawn by NativeChipCard (the cards) and the chip lists (Badge): the neural core cartridge of
    // Core/NativeChipCartridge.cs. The machined parts are baked by Core/NativeChipBake.cs at the drawn pixel size and cached
    // here as textures (once per tier and UI scale, never per frame): the shell (outline, faceted bezel, inlay, seams, corner
    // gems; drawn as a nine-slice, its pieces snapping in as a card assembles), the core (collar, glass dome, crystal), the
    // traces, the connector's pads, the fins, the ring halves, the crest and the badge. Over them the kit animates: the core's
    // breath and light, the light packets running out along the traces, the glass sheen, the ring's bead, Legendary's embers,
    // Mythic's shards, the kits' sparks and card art (Emperor's storm orbit, bolt, arcs and grid; Ansatsuken's seal, headband,
    // ki rings and dojo grid; L44T's binary rain; Siren's void glass, black flames, watching grin, scanlines and tear), and
    // the draft tray's holo scan (the lit edge and comet round the cartridge's outline, the connector powering up, brackets,
    // scan line, halo; the pick's lock). Calm (Reduced effects) holds every loop still. A frame allocates nothing.
    public static class NativeChipArt
    {
        // ---- geometry: the cartridge's outline in a card rect (`radius` picks the tray or the spotlight's proportions) ----
        public static bool Large(float radius)=>radius>=11;
        public static float TopCut(float radius)=>Large(radius)?Cart.TopCutLarge:Cart.TopCut;
        public static float BottomCut(float radius)=>Large(radius)?Cart.BottomCutLarge:Cart.BottomCut;
        /// <summary>The bezel's width (as NativeChipBake.Shell bakes it).</summary>
        public static float BezelOf(float radius)=>Large(radius)?Cart.BezelLarge:Cart.Bezel;
        // An outline grown `grow` px outward keeps its cut lines `grow` px out, so each cut grows by (2 - sqrt 2) grow.
        private const float Grow=.5858f,Root2=1.41421356f;
        private static Rect Inflate(Rect r,float d)=>new Rect(r.x-d,r.y-d,r.width+2*d,r.height+2*d);
        private static Rect Inset(Rect r,float d)=>new Rect(r.x+d,r.y+d,r.width-2*d,r.height-2*d);
        /// <summary>The card's silhouette grown `grow` px, filled.</summary>
        public static void Package(Rect r,float radius,float grow,Color fill)=>Silhouette(Inflate(r,grow),TopCut(radius)+Grow*grow,BottomCut(radius)+Grow*grow,fill);
        /// <summary>The card's outline grown `grow` px, `width` px inside that line.</summary>
        public static void PackageEdge(Rect r,float radius,float grow,Color edge,float width)=>Edge(Inflate(r,grow),TopCut(radius)+Grow*grow,BottomCut(radius)+Grow*grow,edge,width);
        // A length snapped to whole physical pixels.
        private static float Snap(float v)=>Mathf.Round(v*U.S)/Mathf.Max(.01f,U.S);
        private static void Silhouette(Rect q,float tc,float bc,Color c)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;tc=Mathf.Min(Snap(tc),q.height/2);bc=Mathf.Min(Snap(bc),q.height/2);
            U.Fill(Rect.MinMaxRect(q.x,q.y+tc,q.xMax,q.yMax-bc),c);U.Fill(Rect.MinMaxRect(q.x+tc,q.y,q.xMax-tc,q.y+tc),c);U.Fill(Rect.MinMaxRect(q.x+bc,q.yMax-bc,q.xMax-bc,q.yMax),c);
            int pt=Mathf.RoundToInt(tc*U.S),pb=Mathf.RoundToInt(bc*U.S);
            U.Image(new Rect(q.x,q.y,tc,tc),Corner(0,pt,0),c);U.Image(new Rect(q.xMax-tc,q.y,tc,tc),Corner(1,pt,0),c);U.Image(new Rect(q.xMax-bc,q.yMax-bc,bc,bc),Corner(2,pb,0),c);U.Image(new Rect(q.x,q.yMax-bc,bc,bc),Corner(3,pb,0),c);
        }
        private static void Edge(Rect q,float tc,float bc,Color c,float width)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;tc=Mathf.Min(Snap(tc),q.height/2);bc=Mathf.Min(Snap(bc),q.height/2);float t=NativeHudChrome.Snap(width),band=t*U.S*1.15f;
            U.Fill(new Rect(q.x+tc,q.y,q.width-2*tc,t),c);U.Fill(new Rect(q.x+bc,q.yMax-t,q.width-2*bc,t),c);U.Fill(new Rect(q.x,q.y+tc,t,q.height-tc-bc),c);U.Fill(new Rect(q.xMax-t,q.y+tc,t,q.height-tc-bc),c);
            int pt=Mathf.RoundToInt(tc*U.S),pb=Mathf.RoundToInt(bc*U.S);
            U.Image(new Rect(q.x,q.y,tc,tc),Corner(0,pt,band),c);U.Image(new Rect(q.xMax-tc,q.y,tc,tc),Corner(1,pt,band),c);U.Image(new Rect(q.xMax-bc,q.yMax-bc,bc,bc),Corner(2,pb,band),c);U.Image(new Rect(q.x,q.yMax-bc,bc,bc),Corner(3,pb,band),c);
        }
        // A corner piece `px` physical px square (corner 0 TL, 1 TR, 2 BR, 3 BL: the point the cut removes): the inside of the
        // cut (band 0) or a line `band` px thick inside the diagonal, 4x4 supersampled; cached per size.
        private static readonly Dictionary<int,Texture2D> corners=new Dictionary<int,Texture2D>();
        private static Texture2D Corner(int corner,int px,float band)
        {
            px=Mathf.Clamp(px,1,96);int key=corner*10000000+px*10000+Mathf.RoundToInt(band*8);if(corners.TryGetValue(key,out var t)&&t!=null)return t;var c=new Color[px*px];
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                int hit=0;for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++){float fx=x+(sx+.5f)/4,fy=y+(sy+.5f)/4,ux=corner==0||corner==3?fx:px-fx,uy=corner==0||corner==1?fy:px-fy,d=(ux+uy-px)/Root2;if(band<=0?d>=0:d>=0&&d<band)hit++;}
                c[(px-1-y)*px+x]=new Color(1,1,1,hit/16f);
            }
            t=Make(px,px,false);t.SetPixels(c);t.Apply(false,true);corners[key]=t;return t;
        }
        private static Texture2D Make(int w,int h,bool repeat)=>new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=repeat?TextureWrapMode.Repeat:TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};

        // ---- baked layers (Core/NativeChipBake.cs), cached by kind, tier and physical size ----
        private enum Bake{Shell,ShellLarge,Core,Traces,Pad,Fin,RingFront,RingBack,Crest,Badge,Pip}
        private static readonly Dictionary<(Bake,int,int),Texture2D> baked=new Dictionary<(Bake,int,int),Texture2D>();
        private static int TierIndex(string tier){int i=Array.IndexOf(Cart.Tiers,tier);return i>=0?i:tier==Cart.Rainbow?Cart.Tiers.Length:0;}
        private static string TierAt(int i)=>i<Cart.Tiers.Length?Cart.Tiers[i]:Cart.Rainbow;
        private static Texture2D Baked(Bake kind,string tier,int size)
        {
            var key=(kind,TierIndex(tier),size);if(baked.TryGetValue(key,out var t)&&t!=null)return t;if(baked.Count>320)ClearBakes();
            return baked[key]=Upload(Raster(kind,TierAt(key.Item2),size),kind==Bake.Pad);
        }
        // A layer rasterised (pure managed code: safe on the warming worker).
        private static NativeChipBake.Image Raster(Bake kind,string id,int size)
        {
            float s=size/100f;
            return kind==Bake.Shell?NativeChipBake.Shell(id,s,false):kind==Bake.ShellLarge?NativeChipBake.Shell(id,s,true):kind==Bake.Core?NativeChipBake.Core(id,size):kind==Bake.Traces?NativeChipBake.Traces(TraceRadius,s):
                kind==Bake.Pad?NativeChipBake.Pad(s):kind==Bake.Fin?NativeChipBake.Fin(id,s):kind==Bake.RingFront?NativeChipBake.Ring(RingRadius,s,true):kind==Bake.RingBack?NativeChipBake.Ring(RingRadius,s,false):
                kind==Bake.Crest?NativeChipBake.Crest(id,s):kind==Bake.Badge?NativeChipBake.Badge(id,s):PipImage(size);
        }
        private static Texture2D Upload(NativeChipBake.Image im,bool repeat)
        {
            var px=new Color[im.W*im.H];for(int i=0;i<px.Length;i++)px[i]=new Color(im.Rgba[i*4],im.Rgba[i*4+1],im.Rgba[i*4+2],im.Rgba[i*4+3]);
            var t=Make(im.W,im.H,repeat);t.SetPixels(px);t.Apply(false,true);return t;
        }
        // The size keys the draws bake their layers at (shared with Warm, so the warmed layers are the ones drawn).
        private static int CoreKey(float width)=>Mathf.Clamp(Mathf.RoundToInt(width*U.S),16,256);
        private static float TraceStep(float coreR)=>Mathf.Round(coreR/TraceRadius*20)/20;
        private static int RingKey(float R)=>ScaleKey(R/RingRadius);
        private static float CrestScale(float width)=>Mathf.Round(Mathf.Clamp(width/272,.7f,1.6f)*10)/10;
        private static float BadgeScale(Rect r)=>Mathf.Min(r.width/NativeChipBake.BadgeW,r.height/NativeChipBake.BadgeH);
        private static int PipKey(float size)=>Mathf.Clamp(Mathf.RoundToInt(size*U.S),4,32);
        // ---- warming: every tier's layers at the current UI scale (the tray's and the spotlight's sizes), rasterised on a worker
        // as soon as a match is on screen and uploaded a few per call, so the first draft does not stall on a bake (Baked still
        // bakes in place if a card is drawn first). Call every repaint; it does nothing once warm for this scale. ----
        private static System.Threading.Tasks.Task<List<((Bake,int,int) key,NativeChipBake.Image image)>> warming;private static int warmedScale=-1,warmUploaded;
        public static void Warm()
        {
            if(warming==null)
            {
                int scale=ScaleKey();if(scale==warmedScale)return;warmedScale=scale;var jobs=new List<(Bake,int,int)>();
                for(int i=0;i<Cart.Tiers.Length;i++)
                {
                    var l=Cart.Of(Cart.Tiers[i]);jobs.Add((Bake.Shell,i,scale));jobs.Add((Bake.ShellLarge,i,scale));jobs.Add((Bake.Core,i,CoreKey(54)));jobs.Add((Bake.Core,i,CoreKey(84)));
                    if(NativeChipBake.HasCrest(Cart.Tiers[i])){jobs.Add((Bake.Crest,i,ScaleKey(CrestScale(272))));jobs.Add((Bake.Crest,i,ScaleKey(CrestScale(480))));}if(l.Fins)jobs.Add((Bake.Fin,i,scale));
                }
                foreach(float coreR in new[]{27f,42f}){jobs.Add((Bake.Traces,0,ScaleKey(TraceStep(coreR))));jobs.Add((Bake.RingFront,0,RingKey(coreR)));jobs.Add((Bake.RingBack,0,RingKey(coreR)));}
                jobs.Add((Bake.Pad,0,scale));jobs.Add((Bake.Pip,0,PipKey(6)));jobs.Add((Bake.Pip,0,PipKey(7)));jobs.RemoveAll(j=>baked.ContainsKey(j));
                warmUploaded=0;warming=System.Threading.Tasks.Task.Run(()=>{var done=new List<((Bake,int,int),NativeChipBake.Image)>(jobs.Count);foreach(var j in jobs)done.Add((j,Raster(j.Item1,TierAt(j.Item2),j.Item3)));return done;});
                return;
            }
            if(!warming.IsCompleted)return;
            if(warming.IsFaulted){Debug.LogWarning("Chip art failed to warm: "+warming.Exception?.GetBaseException().Message);warming=null;return;}
            var list=warming.Result;
            for(int n=0;n<6&&warmUploaded<list.Count;n++,warmUploaded++){var (key,image)=list[warmUploaded];if(!baked.TryGetValue(key,out var t)||t==null)baked[key]=Upload(image,key.Item1==Bake.Pad);}
            if(warmUploaded>=list.Count)warming=null;
        }
        public static void ClearBakes(){foreach(var t in baked.Values)if(t!=null)UnityEngine.Object.Destroy(t);baked.Clear();}
        // The size key of a layer baked at the current UI scale times `k` (hundredths of a physical px per virtual px).
        private static int ScaleKey(float k=1)=>Mathf.Clamp(Mathf.RoundToInt(U.S*k*100),20,800);
        // The traces and the ring are baked for a reference core radius and drawn scaled to the card's (their lines thin a
        // little on the spotlight card).
        private const float TraceRadius=24,RingRadius=24;
        // The rank pip: a small hexagon (white, tinted).
        private static NativeChipBake.Image PipImage(int px)
        {
            var cv=new NativeChipBake.Canvas(px,px);var hex=Cart.Hex(new System.Numerics.Vector2(px/2f,px/2f),px/2f-.4f,true);
            for(int y=0;y<px;y++)for(int x=0;x<px;x++){float d=NativeChipBake.Poly(new System.Numerics.Vector2(x+.5f,y+.5f),hex);cv.Over(x,y,System.Numerics.Vector3.One,Mathf.Clamp01(.5f-d));}
            return new NativeChipBake.Image{W=px,H=px,Rgba=cv.Pixels()};
        }

        // ---- the shell: the baked nine-slice over the card rect; while a card assembles (`spread` px) each piece snaps in from
        // outside (corners diagonally, edges straight in), the body's middle staying put ----
        public static void Shell(Rect r,string tier,float radius,float spread,float a)
        {
            if(a<=0||Event.current.type!=EventType.Repaint)return;bool large=Large(radius);var tex=Baked(large?Bake.ShellLarge:Bake.Shell,tier,ScaleKey());
            NativeChipBake.ShellSlices(large,out float side,out float top,out float bottom,out float middle);float W=2*side+middle,H=top+middle+bottom;
            var xs=SliceX;var ys=SliceY;var us=SliceU;var vs=SliceV;xs[0]=r.x;xs[1]=r.x+side;xs[2]=r.xMax-side;xs[3]=r.xMax;ys[0]=r.y;ys[1]=r.y+top;ys[2]=r.yMax-bottom;ys[3]=r.yMax;
            us[0]=0;us[1]=side/W;us[2]=(side+middle)/W;us[3]=1;vs[0]=0;vs[1]=top/H;vs[2]=(top+middle)/H;vs[3]=1;var c=new Color(1,1,1,a);
            for(int j=0;j<3;j++)for(int i=0;i<3;i++)
            {
                float dx=(i-1)*spread,dy=(j-1)*spread;var q=Rect.MinMaxRect(xs[i]+dx,ys[j]+dy,xs[i+1]+dx,ys[j+1]+dy);if(q.width<=0||q.height<=0)continue;
                // Texture v runs up from the bottom row.
                U.Slice(q,tex,Rect.MinMaxRect(us[i],1-vs[j+1],us[i+1],1-vs[j]),c);
            }
        }
        // The nine-slice's edges (scratch, so a frame allocates nothing; IMGUI draws on the main thread).
        private static readonly float[] SliceX=new float[4],SliceY=new float[4],SliceU=new float[4],SliceV=new float[4];
        // ---- the body's light: the core's light pooled round it, the connector's light rising from the bottom edge (Siren: the
        // void glass), and the inner shade. `light` the core's strength (breath, charge, ignition) ----
        public static void Body(Rect r,in Cart.Look l,Color rare,float radius,Vector2 core,float coreR,float light,float a,bool plain)
        {
            float bw=BezelOf(radius),tc=TopCut(radius),bc=BottomCut(radius);bool siren=l.Metal==Cart.Metal.Void;var inner=Inset(r,bw);
            if(siren&&!plain)
            {   // radial-gradient(130% 75% at 50% 112%, #3a2166, #160d27 42%, transparent 72%) inside the bezel (clipped: not under a rotation).
                var e=new Rect(r.center.x-1.3f*r.width,r.yMax+.12f*r.height-.75f*r.height,2.6f*r.width,1.5f*r.height);
                for(int b=0;b<3;b++){U.BeginBand(Band(inner,tc,bc,b));U.RadialStops(e,U.Hex(0x160d27,a),Stops3,Alpha2);U.RadialStops(e,U.Hex(0x3a2166,a),Stops2,Alpha1);U.EndBand();}
            }
            // The body's fine hex mesh (inside the bezel, clear of the cuts).
            Ensure();var mesh=Rect.MinMaxRect(r.x+bw,r.y+tc,r.xMax-bw,r.yMax-bc);U.Tile(mesh,hexMesh,new Color(1,1,1,(siren?.02f:.032f)*a),new Vector2(6,6.928f),new Vector2(0,mesh.y-r.y));
            var pool=new Rect(core.x-2.3f*coreR,core.y-2.3f*coreR,4.6f*coreR,4.6f*coreR);U.RadialStops(pool,U.A(rare,(siren?.07f:.12f)*Mathf.Min(1.6f,light)*a),PoolAt,PoolAlpha);
            float lift=Mathf.Min(30,r.height*.22f);U.Fade(Rect.MinMaxRect(r.x+bc+bw,r.yMax-bw-lift,r.xMax-bc-bw,r.yMax-bw),U.A(rare,(siren?.05f:.075f)*a),false);
        }
        private static readonly float[] PoolAt={0,.4f,1},PoolAlpha={1,.4f,0},Stops3={0,.42f,.72f,1},Stops2={0,.42f,1},Alpha2={1,1,0,0},Alpha1={1,0,0};
        // ---- the title plate: a recessed, anodised panel behind the grade and name (right of the core, above the divider) ----
        public static void TitlePlate(Rect plate,in Cart.Look l,Color rare,float a)
        {
            if(plate.width<20||plate.height<10)return;float p=NativeHudChrome.Px;var top=U.Mix(U.Hex(l.Body),rare,.1f);top=Color.Lerp(top,Color.white,.04f);
            NativeHudChrome.Cut(plate,U.A(top,.55f*a),5,NativeHudChrome.Corners.BL|NativeHudChrome.Corners.TR);U.Fade(new Rect(plate.x,plate.y,plate.width,plate.height*.5f),new Color(1,1,1,.03f*a));
            U.Fill(new Rect(plate.x,plate.y,plate.width-5,p),new Color(0,0,0,.5f*a));U.Fill(new Rect(plate.x+5,plate.yMax-p,plate.width-5,p),U.A(Color.Lerp(rare,Color.white,.5f),.12f*a));
        }
        // ---- the traces etched from the core, clipped inside the bezel (not under a rotation), and the sweep's light packets ----
        public static void Traces(Rect r,float radius,Vector2 core,float coreR,Color rare,float a,bool plain)
        {
            if(plain||a<=0)return;
            // Baked at the card's core radius (in steps of a twentieth), so the lines stay one pixel wide.
            float step=TraceStep(coreR),k=TraceRadius*step,bw=BezelOf(radius);var tex=Baked(Bake.Traces,"common",ScaleKey(step));
            var at=new Rect(core.x+NativeChipBake.TraceOrigin*k,core.y+NativeChipBake.TraceOrigin*k,tex.width/U.S,tex.height/U.S);
            U.BeginBand(Rect.MinMaxRect(r.x+bw+1,r.y+TopCut(radius),r.xMax-bw-1,r.yMax-bw-1));U.Image(at,tex,U.A(rare,a));U.EndBand();
        }
        /// <summary>The sweep (NativeChipCartridge.Sweep): a packet of the tier's light running out along each trace, `u` 0..1.</summary>
        public static void Packets(Rect r,float radius,Vector2 core,float coreR,Color light,float u,float a)
        {
            float bw=BezelOf(radius)+2;var inside=Rect.MinMaxRect(r.x+bw,r.y+TopCut(radius),r.xMax-bw,r.yMax-bw);float fade=Mathf.Clamp01(u/.12f)*Mathf.Clamp01((1-u)/.3f);if(fade<=0)return;
            for(int i=0;i<Cart.Traces.Length;i++)
            {
                var q=Cart.OnTrace(i,u);var p=new Vector2(core.x+q.X*coreR,core.y+q.Y*coreR);if(!inside.Contains(p))continue;
                U.Glow(new Rect(p.x-1.5f,p.y-1.5f,3,3),U.A(light,.55f*fade*a),5);U.Glow(new Rect(p.x-.4f,p.y-.4f,.8f,.8f),U.A(Color.Lerp(light,Color.white,.6f),fade*a),1.6f);
            }
        }
        // ---- the edge connector under the card: a dark substrate with the gold pads (a key notch a third along), the tier's
        // light between them; while assembling (`u` < 1) a glint runs along them as they come up ----
        public static Rect ConnectorRect(Rect r,float height,float inset)=>Rect.MinMaxRect(r.x+inset,r.yMax-1,r.xMax-inset,r.yMax+height);
        public static void Connector(Rect r,float radius,float height,float inset,Color rare,float u,float a)
        {
            if(a<=0)return;var c=ConnectorRect(r,height,inset);if(c.width<12)return;float p=Cart.PadPitch;
            NativeHudChrome.Cut(c,new Color(.02f,.025f,.035f,a),2.2f,NativeHudChrome.Corners.BL|NativeHudChrome.Corners.BR);U.Fill(new Rect(c.x+1,c.y+1,c.width-2,1),U.A(rare,.35f*a));
            int n=Mathf.Max(1,Mathf.FloorToInt((c.width-2)/p)),key=Mathf.Clamp(Mathf.RoundToInt(n*.3f),1,Mathf.Max(1,n-2));float x0=c.x+(c.width-n*p)/2,py=c.y+1.2f,ph=height-1.5f;var tex=Baked(Bake.Pad,"common",ScaleKey());var tint=new Color(1,1,1,a);
            U.Tile(new Rect(x0,py,key*p,ph),tex,tint,new Vector2(p,ph));U.Tile(new Rect(x0+(key+1)*p,py,(n-key-1)*p,ph),tex,tint,new Vector2(p,ph));
            if(u<1){float k=Mathf.Clamp01((u-.7f)/.3f);if(k>0&&k<1){float gx=Mathf.Lerp(x0,x0+n*p,k);U.Glow(new Rect(gx-3,py,6,ph),U.A(Color.Lerp(rare,Color.white,.5f),.8f*a),6);}}
        }
        // ---- the fins (Epic and up): swept blades off both sides at mid height, snapping in with the shell ----
        public static void Fins(Rect r,string tier,float spread,float a)
        {
            if(a<=0)return;var tex=Baked(Bake.Fin,tier,ScaleKey());float h=Mathf.Min(NativeChipBake.FinHeight,r.height*.42f),w=NativeChipBake.FinWidth*h/NativeChipBake.FinHeight,y=r.center.y-h/2+4;var c=new Color(1,1,1,a);
            U.Slice(new Rect(r.x-w+1-spread,y,w,h),tex,new Rect(0,0,1,1),c);U.Slice(new Rect(r.xMax-1+spread,y,w,h),tex,new Rect(1,0,-1,1),c);
        }
        // ---- the crest on the top edge's middle (Legendary's crown, Mythic's shards, the kits' marks) ----
        public static void Crest(Rect r,string tier,float radius,float spread,float a)
        {
            if(a<=0||!NativeChipBake.HasCrest(tier))return;float k=CrestScale(r.width);var tex=Baked(Bake.Crest,tier,ScaleKey(k));
            float w=NativeChipBake.CrestWidth*k,h=NativeChipBake.CrestHeight*k;U.Image(new Rect(r.center.x-w/2,r.y-NativeChipBake.CrestRise*k-spread,w,h),tex,new Color(1,1,1,a));
        }
        // ---- the core: the ring's back half, the light spilling round the collar, the baked core, the crystal's breath, the
        // icon projected in it, the ring's front half and bead, and the glass sheen. `light` the core's strength (>1 charged),
        // `hot` 0..1 the charge's push toward white, `sheen` -1..1 the streak's place (NaN none) ----
        public static void Core(Rect core,in Cart.Look l,string icon,Color rare,float light,float hot,float sheen,float t,bool calm,float a)
        {
            if(a<=0)return;float R=core.width/2;var c=core.center;Color glow=U.Hex(l.Core),bright=U.Hex(l.CoreHi);bool ring=l.Ring;float orbit=Cart.Orbit(t,calm);
            if(ring){var back=Baked(Bake.RingBack,"common",RingKey(R));U.Image(RingRect(c,back),back,U.A(Color.Lerp(rare,Color.white,.25f),.6f*a));}
            U.Glow(Inset(core,R*.18f),U.A(glow,Mathf.Min(1,.22f*light)*a),R*.55f);
            U.Image(core,Baked(Bake.Core,l.Tier,CoreKey(core.width)),new Color(1,1,1,a));
            // The crystal breathing: its light swelling inside the window.
            float cr=R*.62f;U.RadialStops(new Rect(c.x-cr,c.y-cr,2*cr,2*cr),U.A(Color.Lerp(glow,bright,.3f),Mathf.Clamp01(.32f+.4f*(light-1))*a),CrystalAt,CrystalAlpha);
            // The icon, projected: a dark bed for contrast, the tier's glow, then the line icon in a pale tint of the core.
            float iw=R*.82f;var ir=new Rect(c.x-iw/2,c.y-iw/2,iw,iw);U.Glow(ir,new Color(0,0,0,.45f*a),R*.12f);U.Glow(ir,U.A(glow,.35f*Mathf.Min(1.4f,light)*a),R*.16f);
            NativeIcons.Draw(ir,icon,U.A(Color.Lerp(Color.Lerp(bright,Color.white,.55f),Color.white,hot),a));
            if(ring)
            {
                var front=Baked(Bake.RingFront,"common",RingKey(R));U.Image(RingRect(c,front),front,U.A(Color.Lerp(rare,Color.white,.3f),.85f*a));
                // The bead: round the ellipse, bigger and brighter on the near side.
                float cs=Mathf.Cos(NativeChipBake.RingTilt),sn=Mathf.Sin(NativeChipBake.RingTilt),ex=Mathf.Cos(orbit)*NativeChipBake.RingX*R,ey=Mathf.Sin(orbit)*NativeChipBake.RingY*R;
                var b=new Vector2(c.x+ex*cs-ey*sn,c.y+ex*sn+ey*cs);float near=.5f+.5f*Mathf.Sin(orbit),s=1.2f+1.2f*near;
                U.Glow(new Rect(b.x-s,b.y-s,2*s,2*s),U.A(rare,(.4f+.5f*near)*a),5);U.Fill(new Rect(b.x-s*.5f,b.y-s*.5f,s,s),U.A(Color.Lerp(rare,Color.white,.7f),(.5f+.5f*near)*a));
            }
            if(!float.IsNaN(sheen))
            {   // A narrow diagonal streak across the dome, its length the dome's chord at that offset (it stays on the glass).
                float off=sheen*R*.7f,chord=Mathf.Sqrt(Mathf.Max(0,R*R*.5f-off*off));if(chord>1){U.Push(c,Vector2.zero,1,-38);U.HGradient(new Rect(c.x+off-1.5f,c.y-chord,1.5f,2*chord),Color.clear,new Color(1,1,1,.38f*a));U.HGradient(new Rect(c.x+off,c.y-chord,2.5f,2*chord),new Color(1,1,1,.38f*a),Color.clear);U.Pop();}
            }
        }
        private static readonly float[] CrystalAt={0,.5f,.85f,1},CrystalAlpha={.08f,.3f,1,0};
        // The ring's image is centred on the core at its baked size.
        private static Rect RingRect(Vector2 c,Texture2D t){float s=t.width/U.S;return new Rect(c.x-s/2,c.y-s/2,s,s);}
        // ---- particles: Legendary's embers rising off the crest and top edge; Mythic's shards drifting round the bezel ----
        public static void Embers(Rect r,in Cart.Look l,float t,bool calm,float a)
        {
            int n=Cart.Embers(l);if(n==0||calm||a<=0)return;Color hot=U.Hex(0xffe3a8),glow=U.Hex(0xffae55);
            for(int i=0;i<n;i++)
            {
                float k=Cart.Life(t,i,2.6f),op=Cart.LifeAlpha(k);float x=r.center.x+(((i*37+11)%100)/100f-.5f)*r.width*.7f+6*Mathf.Sin(t*1.7f+i),y=r.y+2-34*k;float s=2.4f*(1-.55f*k);
                U.Glow(new Rect(x-s*.5f,y-s*.5f,s,s),U.A(glow,.55f*op*a),3.5f);U.Glow(new Rect(x-.4f,y-.4f,.8f,.8f),U.A(hot,op*a),1.4f);
            }
        }
        public static void Shards(Rect r,float radius,in Cart.Look l,float t,bool calm,float a)
        {
            int n=Cart.Shards(l);if(n==0||calm||a<=0)return;Color lit=U.Hex(l.CoreHi),deep=U.Hex(l.Core);
            for(int i=0;i<n;i++)
            {
                float k=Cart.Life(t,i,3.4f),op=Cart.LifeAlpha(k);float u=((i*0.381966f)%1+.13f*k)%1;float tc=TopCut(radius),bc=BottomCut(radius);var p=Cart.Along(r.width,r.height,tc,bc,u*Cart.Perimeter(r.width,r.height,tc,bc),-5-14*k);
                var at=new Vector2(r.x+p.X,r.y+p.Y);float s=3.2f*(1-.4f*k);U.Push(at,Vector2.zero,1,45+200*k*(i%2==0?1:-1));
                U.Glow(new Rect(at.x-s*.6f,at.y-s*.6f,s*1.2f,s*1.2f),U.A(deep,.45f*op*a),4);U.Fill(new Rect(at.x-s*.5f,at.y-s,s,2*s),U.A(deep,op*a));U.Fill(new Rect(at.x-s*.5f,at.y-s,s*.5f,2*s),U.A(lit,op*a));U.Pop();
            }
        }
        // ---- the chip lists' badge: the whole cartridge small, `r` its box (BadgeW x BadgeH proportions) ----
        public static void Badge(Rect r,string tier,float a)
        {
            if(a<=0||Event.current.type!=EventType.Repaint)return;float k=BadgeScale(r);var tex=Baked(Bake.Badge,tier,ScaleKey(k));
            float w=NativeChipBake.BadgeW*k,h=NativeChipBake.BadgeH*k;var b=new Rect(r.center.x-w/2,r.center.y-h/2,w,h);
            U.Glow(new Rect(b.x+w*.3f,b.y+h*.25f,w*.4f,h*.35f),U.A(U.Hex(Cart.Of(tier).Core),.35f*a),w*.35f);U.Image(b,tex,new Color(1,1,1,a));
        }
        /// <summary>The rank pips (1-6) as small hexagons from `x` along the row centred on `y`: lit in the tier, the rest dim.</summary>
        public static float Pips(float x,float y,int rank,Color rare,float size,float a)
        {
            if(rank<=0)return x;var tex=Baked(Bake.Pip,"common",PipKey(size));
            for(int i=0;i<6;i++){var q=new Rect(x+i*(size+1.5f),y-size/2,size,size);if(i<rank){U.Glow(q,U.A(rare,.35f*a),2);U.Image(q,tex,U.A(rare,a));}else U.Image(q,tex,new Color(1,1,1,.13f*a));}
            return x+6*(size+1.5f);
        }
        // ---- the face's light sweep (hover, or Legendary/Mythic's gleam): a soft band crossing `band` left to right ----
        public static void Sweep(Rect band,float u,float a)
        {
            float bw=band.width*.38f,bx=band.x+band.width*(-1.3f+4.6f*u)*.38f;var clip=Rect.MinMaxRect(Mathf.Max(band.x,bx),band.y,Mathf.Min(band.xMax,bx+bw),band.yMax);if(clip.width<=0)return;
            float x0=bx,w=bw;void Seg(float f0,float f1,float a0,float a1){var q=Rect.MinMaxRect(Mathf.Max(clip.x,x0+w*f0),clip.y,Mathf.Min(clip.xMax,x0+w*f1),clip.yMax);if(q.width>0)U.HGradient(q,new Color(1,1,1,a0*a),new Color(1,1,1,a1*a));}
            Seg(0,.4f,0,.06f);Seg(.4f,.5f,.06f,.15f);Seg(.5f,.62f,.15f,0);
        }
        // Mythic: the holographic foil drifting across a band of the face over 7 s (pink-gold-green-blue-violet, 300% wide).
        private static readonly Color[] Holo={U.Hex(0xff6475,.2f),U.Hex(0xffd36e,.16f),U.Hex(0x7dffb2,.14f),U.Hex(0x6ec8ff,.19f),U.Hex(0xc58bff,.2f),U.Hex(0xff6475,.2f)};
        public static void Holographic(Rect band,float t,float a)
        {
            U.BeginBand(band);float span=band.width*3,x=band.x-span*Mathf.Repeat(t/7,1);
            for(int rep=0;rep<2;rep++)for(int i=0;i<5;i++){var q=new Rect(x+rep*span+span*i/5f,band.y,span/5f,band.height);if(q.xMax<band.x||q.x>band.xMax)continue;U.HGradient(q,U.A(Holo[i],a),U.A(Holo[i+1],a));}
            U.EndBand();
        }


        // ---- the exclusive kits' card art (over the body and the traces, under the core, the text and the bezel's light) ----
        // Art that reaches a cut corner is drawn once per face band, so it stays inside the cartridge: band 0 the top strip
        // between the shoulder cuts, 1 the full width between the cuts, 2 the bottom strip between the lower cuts.
        private static Rect Band(Rect r,float tc,float bc,int i)=>i==0?Rect.MinMaxRect(r.x+tc,r.y,r.xMax-tc,r.y+tc):i==1?Rect.MinMaxRect(r.x,r.y+tc,r.xMax,r.yMax-bc):Rect.MinMaxRect(r.x+bc,r.yMax-bc,r.xMax-bc,r.yMax);
        public static void Aura(Rect r,float radius,string tier,float t,float a,bool plain)
        {
            if(plain)return;float bw=BezelOf(radius),tc=Snap(TopCut(radius)),bc=Snap(BottomCut(radius));var face=Inset(r,bw);
            switch(tier){case "mishima":Emperor(r,face,tc,bc,t,a);break;case "ansatsuken":Fighting(r,face,tc,bc,t,a);break;case "l44t":Rain(face,tc,bc,t,a);break;case "siren":Siren(r,face,tc,bc,t,a);break;}
        }
        // emperor-card.ts: a red current orbiting the package edge (conic, 3.2 s), the storm bolt, three current arcs and a grid.
        private static readonly Vector2[] Arc={new Vector2(0,.12f),new Vector2(.24f,.36f),new Vector2(.43f,.21f),new Vector2(.65f,.53f),new Vector2(.76f,.45f),new Vector2(1,.74f),new Vector2(.76f,.49f),new Vector2(.65f,.59f),new Vector2(.43f,.26f),new Vector2(.24f,.41f)};
        private static readonly Vector2[] ArcFlip={new Vector2(1,.88f),new Vector2(.76f,.64f),new Vector2(.57f,.79f),new Vector2(.35f,.47f),new Vector2(.24f,.55f),new Vector2(0,.26f),new Vector2(.24f,.51f),new Vector2(.35f,.41f),new Vector2(.57f,.74f),new Vector2(.76f,.59f)};
        private static readonly float[] Flat={0,1},Full={1,1};
        private static string bolt,seal,face,flames;
        private static void Emperor(Rect r,Rect face,float tc,float bc,float t,float a)
        {
            BuildMarkup();Ensure();
            for(int b=0;b<3;b++)
            {
                U.BeginBand(Band(face,tc,bc,b));U.Tile(face,diagonal,U.Hex(0x90929e,.063f*a),new Vector2(DiagonalTile,DiagonalTile));
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
            float turn=360*Mathf.Repeat(t/3.2f,1);var ring=Inset(face,.5f);
            for(int side=0;side<4;side++)
            {
                bool horizontal=side%2==0;float from=side==0?tc+2:side==1?tc+2:side==2?bc+2:bc+2,to=side==0?tc+2:side==1?bc+2:side==2?bc+2:tc+2,len=(horizontal?ring.width:ring.height)-from-to;int n=Mathf.Max(1,Mathf.CeilToInt(len/5));float step=len/n;
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
        private static void Fighting(Rect r,Rect face,float tc,float bc,float t,float a)
        {
            BuildMarkup();float gh=r.height*.36f,cy=r.yMax-gh/2,cos=Mathf.Cos(25*Mathf.Deg2Rad),sin=Mathf.Sin(25*Mathf.Deg2Rad);
            float k2=Mathf.PingPong(t/1.4f,1),flow=Smooth(k2),tilt=Mathf.Tan(2.5f*Mathf.Deg2Rad)*r.width*1.2f;var band=new Rect(r.x-r.width*.1f+8*flow,r.y+3-tilt/2,r.width*1.2f,8+tilt);float op=(.8f+.2f*flow)*a;
            for(int b=0;b<3;b++)
            {
                U.BeginBand(Band(face,tc,bc,b));
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
            var centre=new Vector2(r.xMax-40,r.yMax-30);U.BeginBand(Band(face,tc,bc,1));
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
        private static void Rain(Rect r,float tc,float bc,float t,float a)
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
                    if((q.x<r.x+tc||q.xMax>r.xMax-tc)&&q.y<r.y+tc||(q.x<r.x+bc||q.xMax>r.xMax-bc)&&q.yMax>r.yMax-bc){U.EndBand();for(int b=0;b<3;b++){U.BeginBand(Band(r,tc,bc,b));U.Text(q,bit,15,c,TextAnchor.MiddleLeft,mono:true);U.EndBand();}U.BeginBand(r);}
                    else U.Text(q,bit,15,c,TextAnchor.MiddleLeft,mono:true);
                }
            }
            U.EndBand();
        }
        // siren-card.ts sirenAura: black flame tongues licking up the bottom, the grin watching from the fire, scanlines, and on
        // the glitch beat (65-72% of 4.2 s) the grin flickers and a tear crosses the package.
        private static void Siren(Rect r,Rect inner,float tc,float bc,float t,float a)
        {
            BuildMarkup();Ensure();float g=Mathf.Repeat(t,4.2f)/4.2f;
            // .siren-flames: left -4%, width 108%, the bottom 42%, its 200 x 70 view box sliced to the box's width (xMidYMax).
            // The tongues' siren-lick, as one slow breath of the row's height (no transform inside the clip). It only ever shrinks the
            // drawn height, so the raster size (the width) and its cached texture stay the same every frame.
            var box=new Rect(r.x-r.width*.04f,r.yMax-r.height*.42f+2,r.width*1.08f,r.height*.42f);float s=Mathf.Max(box.width/200,box.height/70),lick=.95f+.05f*Mathf.Sin(t*2*Mathf.PI/1.7f);
            // .siren-face: 70% wide at right -6%, bottom 2%, turned -6deg; siren-watch flickers .16 → .42 / .03 / .32 on the beat.
            float watch=g>=.65f&&g<.66f?.42f:g>=.66f&&g<.68f?.03f:g>=.68f&&g<.72f?.32f:.16f,fw=r.width*.7f;
            for(int b=0;b<3;b++)
            {
                var clip=Band(inner,tc,bc,b);U.BeginBand(clip);
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
        // ---- textures the kit art and the holo scan share (built on first use) ----
        private static Texture2D scan,diagonal,hexMesh;private const float DiagonalTile=46.669f;
        private static void Ensure()
        {
            if(scan!=null)return;
            // Scanlines: a line every 3 px down the card (row 0 is the bottom; the line on the top texels).
            scan=Make(1,12,true);var px=new Color[12];for(int y=0;y<12;y++)px[y]=new Color(1,1,1,y>=8?1:0);scan.SetPixels(px);scan.Apply(false,true);
            // .storm-grid: a 1 px diagonal every 33 px; a 46.67 px tile (33 * sqrt 2) holds two whole periods, so it repeats seamlessly.
            // The body's hex mesh: hexagon outlines 6 px across the flats (4 texels per px), a period of 6 x 6.93 px.
            hexMesh=Make(24,28,true);px=new Color[24*28];for(int y=0;y<28;y++)for(int x=0;x<24;x++){float fx=(x+.5f)/4,fy=(y+.5f)/4;float best=9;for(int i=-1;i<=2;i++)for(int j=-1;j<=2;j++){float cx=i*6+(j%2!=0?3:0),cy=j*3.464f;float dx=Mathf.Abs(fx-cx),dy=Mathf.Abs(fy-cy);float d=Mathf.Max(dx*.866f+dy*.5f,dy);best=Mathf.Min(best,Mathf.Abs(d-2.6f));}px[y*24+x]=new Color(1,1,1,Mathf.Clamp01(1-best/.45f));}hexMesh.SetPixels(px);hexMesh.Apply(false,true);
            diagonal=Make(64,64,true);px=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float g=(x+(63-y)+1)*(DiagonalTile/64)/1.41421f,d=Mathf.Repeat(g,33);px[y*64+x]=new Color(1,1,1,Mathf.Clamp01(1-Mathf.Abs(d-32)/.7f));}diagonal.SetPixels(px);diagonal.Apply(false,true);
        }

        // ---- the holo scan: the draft tray's highlighted card (NativeHoloScan's timeline; NativeChipCard, NativeDraftScreen) ----
        // A tier-lit edge traced round the cartridge's outline both ways from the middle of the top-left shoulder, then one comet
        // a lap; the connector's pads powering up in the tier's colour as the trace reaches them (brighter as the comet runs
        // by); corner brackets that snap in outside the card over a dark keyline (so they read over the pilot's light torso); one
        // scan line down the face under the text, over faint 3 px holo lines; and a halo of the tier's light behind the card (the
        // core's charge is NativeChipCard's). `k` is the level times the card's alpha, `age` seconds since it engaged, `t` the
        // screen clock. Calm (Reduced effects) keeps the still parts. Every line is at least one physical pixel; nothing allocates.
        private struct HoloTier {public Color Edge,Head,Halo,Bracket,Tick,Scan,Pin;public float HaloAlpha,BracketGlow;public Color[] Tail;public bool Glitch,Blink,Foil;}
        private static readonly Color[] GoldTail={U.Hex(0xfff2cb),U.Hex(0xffc96d),U.Hex(0xf39a35)},FoilTail={U.Hex(0xffb3bd),U.Hex(0xffe39a),U.Hex(0xa8ffd0),U.Hex(0x9fd8ff),U.Hex(0xe2b6ff)};
        private static readonly Color GlitchCyan=U.Hex(0x2de2ff),Keyline=U.Hex(0x05060a);
        // Per tier: the edge line, the comet head (Legendary's gold and Mythic's foil tail), the halo and its strength, the brackets
        // (Mythic's four foil corners) with their glow and corner ticks, the scan and the lit pads; L44T's brackets blink once as
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
        // The outline's cuts snapped as the edge draws them, and the 1.5 px line, so the traced stretches and the comet meet the
        // finished ring exactly.
        private static void EdgeOf(float radius,out float tc,out float bc,out float w){tc=Snap(TopCut(radius));bc=Snap(BottomCut(radius));w=NativeHudChrome.Snap(1.5f);}
        private static Color Cycle(float u,Color[] c){float x=Mathf.Repeat(u,1)*c.Length;int i=(int)x%c.Length;return Color.Lerp(c[i],c[(i+1)%c.Length],x-Mathf.Floor(x));}
        // Behind the card: the halo of the tier's light (breathing, NativeHoloScan.Swell) and a tight glow of its edge.
        public static void HoloHalo(Rect r,string tier,Color rare,float k,float t,bool calm)
        {
            var h=HoloOf(tier,rare);U.Glow(r,U.A(h.Halo,h.HaloAlpha*k*NativeHoloScan.Swell(t,calm)),40);U.Glow(r,U.A(h.Edge,.16f*k),10);
        }
        // On the card's border: the trace, then the finished ring (Mythic's drifting foil) and the comet with its bead.
        public static void HoloEdge(Rect r,float radius,string tier,Color rare,float k,float age,float t,bool calm)
        {
            var h=HoloOf(tier,rare);EdgeOf(radius,out float tc,out float bc,out float w);var q=r;float L=Cart.Perimeter(q.width,q.height,tc,bc),start=L-tc*Root2/2,ring=.34f*k;
            // Siren: the edge jolts on the glitch beat, a cyan copy on the other side.
            float jolt=h.Glitch&&!calm?Jolt(t)*.75f:0;if(jolt!=0){Edge(new Rect(q.x-jolt,q.y,q.width,q.height),tc,bc,U.A(GlitchCyan,.35f*k),1.5f);q.x+=jolt;}
            Color edge=h.Foil&&!calm?Cycle(t/7,FoilTail):h.Edge;float reach=NativeHoloScan.TraceReach(age,calm);
            if(reach>=1)Edge(q,tc,bc,U.A(edge,ring),1.5f);
            else
            {   // The trace: both ways from the middle of the top-left shoulder, its leading tenth ramping up to the head, a bead on each end.
                float half=reach*L/2,lead=Mathf.Min(half,L*.1f);Stretch(q,tc,bc,w,start-half,start+half,ring,ring,edge);
                Stretch(q,tc,bc,w,start+half-lead,start+half,0,.9f*k,h.Head);Stretch(q,tc,bc,w,start-half,start-half+lead,.9f*k,0,h.Head);Bead(Along(q,tc,bc,w,start+half),h.Head,k);Bead(Along(q,tc,bc,w,start-half),h.Head,k);
            }
            // The comet: from where the trace closed, clockwise once a lap, its tail (twice the line) growing in; Legendary's gold
            // and Mythic's foil in bands.
            if(!NativeHoloScan.Comet(age,calm,out float head,out float tail))return;float hd=start+head*L,len=tail*L+.01f;
            if(h.Tail==null)Stretch(q,tc,bc,2*w,hd-len,hd,0,k,h.Head);
            else{int n=h.Tail.Length;for(int s=0;s<n;s++)Stretch(q,tc,bc,2*w,hd-len*(s+1f)/n,hd-len*s/(float)n,k*(1-(s+1f)/n),k*(1-s/(float)n),h.Tail[s]);}
            Bead(Along(q,tc,bc,w,hd),h.Head,k);
        }
        // A round white-hot bead in a glow of the head colour.
        private static void Bead(Vector2 p,Color head,float k){U.Glow(new Rect(p.x-2,p.y-2,4,4),U.A(head,.7f*k),8);U.Glow(new Rect(p.x-.8f,p.y-.8f,1.6f,1.6f),U.A(Color.Lerp(head,Color.white,.6f),Mathf.Min(1,k)),2.2f);}
        // The outline's eight runs clockwise from the top edge's left end: top, the top-right shoulder, right, the bottom-right
        // cut, bottom, the bottom-left cut, left, the top-left shoulder.
        private static float RunLength(Rect q,float tc,float bc,int i)=>i==0?q.width-2*tc:i==4?q.width-2*bc:i==2||i==6?q.height-tc-bc:(i==1||i==7?tc:bc)*Root2;
        private static Vector2 Along(Rect q,float tc,float bc,float w,float d)
        {
            var p=Cart.Along(q.width,q.height,tc,bc,d,w/2);return new Vector2(q.x+p.X,q.y+p.Y);
        }
        // The outline from d0 to d1 (any range, wrapping) at alpha a0 → a1 along it, `w` thick inside the border.
        private static void Stretch(Rect q,float tc,float bc,float w,float d0,float d1,float a0,float a1,Color c)
        {
            float L=Cart.Perimeter(q.width,q.height,tc,bc);if(d1-d0<=.01f||L<=0)return;if(d1-d0>L)d0=d1-L;float shift=Mathf.Floor(d0/L)*L;d0-=shift;d1-=shift;
            if(d1<=L){Runs(q,tc,bc,w,d0,d1,a0,a1,c);return;}float am=Mathf.Lerp(a0,a1,(L-d0)/(d1-d0));Runs(q,tc,bc,w,d0,L,a0,am,c);Runs(q,tc,bc,w,0,d1-L,am,a1,c);
        }
        private static void Runs(Rect q,float tc,float bc,float w,float d0,float d1,float a0,float a1,Color c)
        {
            float at=0;
            for(int p=0;p<8;p++)
            {
                float len=RunLength(q,tc,bc,p),s0=Mathf.Max(d0,at),s1=Mathf.Min(d1,at+len);
                if(s1>s0&&len>0)
                {
                    float b0=Mathf.Lerp(a0,a1,(s0-d0)/(d1-d0)),b1=Mathf.Lerp(a0,a1,(s1-d0)/(d1-d0)),u0=s0-at,u1=s1-at;
                    switch(p)
                    {
                        case 0:Ramp(new Rect(q.x+tc+u0,q.y,u1-u0,w),true,b0,b1,c);break;
                        case 2:Ramp(new Rect(q.xMax-w,q.y+tc+u0,w,u1-u0),false,b0,b1,c);break;
                        case 4:Ramp(new Rect(q.xMax-bc-u1,q.yMax-w,u1-u0,w),true,b1,b0,c);break;
                        case 6:Ramp(new Rect(q.x,q.yMax-bc-u1,w,u1-u0),false,b1,b0,c);break;
                        default:Diagonal(q,tc,bc,w,p,(u1-u0)/len*(b0+b1)/2,c);break;
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
        // A cut's diagonal (run 1 top right, 3 bottom right, 5 bottom left, 7 top left): its line drawn whole at the stretch's
        // mean alpha over the share of it lit (the trace crosses a cut within a frame, the comet in a few).
        private static void Diagonal(Rect q,float tc,float bc,float w,int run,float al,Color c)
        {
            if(al<=.002f)return;float s=run==1||run==7?tc:bc;int corner=run==7?0:run==1?1:run==3?2:3;
            var box=corner==0?new Rect(q.x,q.y,s,s):corner==1?new Rect(q.xMax-s,q.y,s,s):corner==2?new Rect(q.xMax-s,q.yMax-s,s,s):new Rect(q.x,q.yMax-s,s,s);
            U.Image(box,Corner(corner,Mathf.RoundToInt(s*U.S),w*U.S*1.15f),U.A(c,Mathf.Min(1,al)));
        }
        // How far the trace has run along the bottom edge to the pad at `x` (from the card's left edge): down the left side
        // (the anticlockwise arm) or round the top and the right side (the clockwise arm), whichever is nearer.
        private static float PadDistance(Rect q,float tc,float bc,float x)
        {
            float half=tc*Root2/2,left=half+(q.height-tc-bc)+bc*Root2+(x-bc),right=half+(q.width-2*tc)+tc*Root2+(q.height-tc-bc)+bc*Root2+(q.width-bc-x);return Mathf.Min(left,right);
        }
        // The connector powers up as the trace reaches its pads (calm: all lit, still): a glow along the lit span and the pads'
        // light, a pad flaring as the comet runs by on the bottom edge.
        public static void HoloPads(Rect r,float radius,float height,float inset,string tier,Color rare,float k,float age,bool calm)
        {
            var h=HoloOf(tier,rare);EdgeOf(radius,out float tc,out float bc,out _);var c=ConnectorRect(r,height,inset);if(c.width<12)return;
            float reach=NativeHoloScan.TraceReach(age,calm),L=Cart.Perimeter(r.width,r.height,tc,bc),run=Mathf.Clamp01(reach)*L/2;float x0=c.x-r.x,x1=c.xMax-r.x;
            float lit0=calm?1:Mathf.Clamp01((run-PadDistance(r,tc,bc,x0))/NativeHoloScan.Ramp),lit1=calm?1:Mathf.Clamp01((run-PadDistance(r,tc,bc,x1))/NativeHoloScan.Ramp);
            // The lit span: from the left while the left arm has reached it, from the right for the right arm (they meet in the middle).
            float leftTo=calm?x1:Mathf.Clamp(bc+run-(tc*Root2/2+(r.height-tc-bc)+bc*Root2),x0,x1),rightFrom=calm?x1:Mathf.Clamp(r.width-bc-(run-(tc*Root2/2+(r.width-2*tc)+tc*Root2+(r.height-tc-bc)+bc*Root2)),x0,x1);
            Color hot=Color.Lerp(h.Pin,Color.white,.35f);
            if(leftTo>x0){var q=Rect.MinMaxRect(r.x+x0,c.y+1,r.x+leftTo,c.yMax-.5f);U.Glow(q,U.A(h.Pin,.22f*k),5);U.Fill(q,U.A(hot,.28f*k*Mathf.Max(lit0,.4f)));}
            if(rightFrom<x1){var q=Rect.MinMaxRect(r.x+Mathf.Max(rightFrom,leftTo),c.y+1,r.x+x1,c.yMax-.5f);if(q.width>0){U.Glow(q,U.A(h.Pin,.22f*k),5);U.Fill(q,U.A(hot,.28f*k*Mathf.Max(lit1,.4f)));}}
            if(NativeHoloScan.Comet(age,calm,out float head,out _))
            {   // The comet's bead crossing the bottom edge: where along it, a flare under it.
                float d=Mathf.Repeat(L-tc*Root2/2+head*L,L),bottomFrom=BottomRun(r,tc,bc);if(d>=bottomFrom&&d<=bottomFrom+r.width-2*bc){float x=r.xMax-bc-(d-bottomFrom);if(x>=c.x&&x<=c.xMax)U.Glow(new Rect(x-4,c.y+1,8,c.height-1.5f),U.A(hot,.6f*k),6);}
            }
        }
        // Where the bottom run starts along the outline.
        private static float BottomRun(Rect r,float tc,float bc)=>(r.width-2*tc)+tc*Root2+(r.height-tc-bc)+bc*Root2;
        // The scan: faint 3 px holo lines while lit (Siren has its own) and the line's one pass down the face, a trailing sheen
        // above it, clipped to the face clear of the cuts; calm: a still sheen down from the top edge instead of the pass.
        public static void HoloScan(Rect r,float radius,string tier,Color rare,float k,float age,bool calm)
        {
            var h=HoloOf(tier,rare);Ensure();float tc=Snap(TopCut(radius)),bc=Snap(BottomCut(radius));var face=Rect.MinMaxRect(r.x+1.5f,r.y+tc,r.xMax-1.5f,r.yMax-bc);if(!h.Glitch)U.Tile(face,scan,U.A(h.Scan,.03f*k),new Vector2(1,3));
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
        // Four square-ended L brackets `off` px outside the card's corners (the arm clear of the crest and the connector), each
        // over a dark keyline, with a soft glow and a corner tick; Mythic's in four foil colours.
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
        // face (the core's flare is NativeChipCard's); calm: the brackets stay at rest and fade, a softer ring, no light.
        public static void HoloLock(Rect r,float radius,string tier,Color rare,float age,bool calm,float a)
        {
            if(age<0||age>=NativeHoloScan.Lock||a<=0)return;var h=HoloOf(tier,rare);float u=age/NativeHoloScan.Lock,op=(1-u)*a;
            Brackets(r,h,NativeHoloScan.LockOffset(age,calm),op,0,true);PackageEdge(r,radius,0,U.A(h.Head,(calm?.5f:.9f)*op),1.5f);if(!calm)Package(r,radius,0,U.A(Color.white,.14f*(1-u)*(1-u)*a));
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
