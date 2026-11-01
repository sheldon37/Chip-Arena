using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A soft point light of the rooftop scene (source frame): its centre, reach (metres), HDR colour and wrap (0..1);
    /// a screen's light names its screen (it breathes with it: NativeRooftopCity.ScreenLight).</summary>
    public struct RooftopLight {public Vector3 Position;public float Reach,Wrap;public Vector3 Color;public string Screen;}
    // The rooftop city's architecture (owner 2026-10-29: "Rework main menu background. Improve the graphics immensely~ utilize
    // lighting ... Improve building design (It still appears "voxels") and flat; Add exceptional detail to make a beautiful
    // display"). Presentation only, deterministic, built once. Every tower is a style of its own (NativeRooftopFacade.shader
    // draws its bays and rooms):
    //  - glazed: a curtain-wall tower, often with cut corners, its edges in fins, a lantern and a mast on top;
    //  - residential: punched windows, balconies down the faces the camera sees, air-con boxes, a water tank and a billboard;
    //  - corporate: two to four setbacks on cornice ledges, slab lights, neon corners, a helipad or a crown ring, a mast;
    //  - masonry: an old mid-rise with a stepped cornice, a fire escape, a hanging vertical sign and a water tank.
    // Detail follows what the loop's camera sees: the lots in view within 300 m take everything, to 750 m their massing,
    // ledges, parapets, plant and signs, farther and out of view their massing alone. The actors' roofs (the duel, the
    // runners', the screen towers) stay bare and flat. Our own roof is a membrane, a parapet with its coping and a cyan strip,
    // HVAC plant, a vent stack, a lattice mast with its dish, a stair house with a lit door, a water tank, pipes on sleepers and
    // a string of lamps (Perch). The eight lights (Lights) are the screens', the corner sign's, the lip strip's and our lamps'.
    public static partial class NativeRooftopCity
    {
        // The modes the newer shaders draw (the Runtime draws each range with its own material): a tower facade
        // (NativeRooftopFacade: Attr seed, style, hue, lit share; W the tower's top) and a lit prop (NativeRooftopProp: Attr the
        // linear albedo, HDR on signs and lamps, and the material).
        public const int Tower=10,Prop=20;
        public const int Concrete=0,Metal=1,Grille=2,SignPanel=3,Membrane=4,Glass=5,Lamp=6;
        public const int Glazed=0,Residential=1,Corporate=2,Masonry=3;
        public const int LightCount=8;
        public const float ViewFacing=.62f,NearDetail=300,MidDetail=750;

        private static Vector4 Mat(float r,float g,float b,int material)=>new Vector4(r,g,b,material);
        private static readonly Vector4 DarkConcrete=Mat(.050f,.052f,.065f,Concrete),PaleConcrete=Mat(.085f,.085f,.095f,Concrete),RoofSkin=Mat(.034f,.036f,.046f,Membrane),
            Steel=Mat(.060f,.068f,.085f,Metal),DarkSteel=Mat(.028f,.032f,.042f,Metal),Vent=Mat(.050f,.055f,.070f,Grille),Rust=Mat(.080f,.045f,.030f,Metal);

        // A vertical wall from a to b (xz; y ignored) between heights y0 and y1, its outside on the right walking a->b seen from
        // above (the normal (b - a) x +y), face coordinates metric along it (u = p . (+y x n)) and up (the world height).
        public static void Wall(World w,Vector3 a,Vector3 b,float y0,float y1,int mode,Vector4 attr,float top=0)
        {
            var d=new Vector3(b.X-a.X,0,b.Z-a.Z);float len=d.Length();if(len<1e-4f||y1-y0<1e-4f)return;
            var n=Vector3.Normalize(Vector3.Cross(d,Vector3.UnitY));var ua=Vector3.Cross(Vector3.UnitY,n);float u0=a.X*ua.X+a.Z*ua.Z,u1=b.X*ua.X+b.Z*ua.Z;
            w.Quads.Add(new RooftopQuad{A=new Vector3(a.X,y0,a.Z),B=new Vector3(b.X,y0,b.Z),C=new Vector3(b.X,y1,b.Z),D=new Vector3(a.X,y1,a.Z),Normal=n,Uv=new Vector4(u0,y0,u1,y1),Mode=mode,Attr=attr,W=top});
        }
        // A footprint, counter-clockwise about +y as Box's faces run (front +z, right +x, back -z, left -x), its corners cut by
        // `chamfer`.
        public static Vector3[] Footprint(float x0,float z0,float x1,float z1,float chamfer=0)
        {
            if(chamfer<=.01f)return new[]{new Vector3(x0,0,z1),new Vector3(x1,0,z1),new Vector3(x1,0,z0),new Vector3(x0,0,z0)};
            float c=Math.Min(chamfer,Math.Min(x1-x0,z1-z0)*.45f);
            return new[]{new Vector3(x0+c,0,z1),new Vector3(x1-c,0,z1),new Vector3(x1,0,z1-c),new Vector3(x1,0,z0+c),new Vector3(x1-c,0,z0),new Vector3(x0+c,0,z0),new Vector3(x0,0,z0+c),new Vector3(x0,0,z1-c)};
        }
        // A regular polygon footprint (an octagonal tank or stack), counter-clockwise about +y.
        public static Vector3[] Ring(Vector3 c,float r,int sides,float turn=0)
        {
            var p=new Vector3[sides];for(int k=0;k<sides;k++){float a=turn+2*(float)Math.PI*k/sides;p[k]=new Vector3(c.X+r*(float)Math.Cos(a),0,c.Z-r*(float)Math.Sin(a));}
            return p;
        }
        // A horizontal cap over a convex footprint at height y, facing +y (or -y underneath): a fan from its first corner, two
        // triangles to a quad (a box's top is one quad, an octagon three), its corners' xz as metric face coordinates.
        public static void Cap(World w,Vector3[] fp,float y,int mode,Vector4 attr,bool under=false)
        {
            int n=fp.Length;Vector3 P(int k){var p=fp[under?(n-k)%n:k];p.Y=y;return p;}
            for(int k=1;k+1<n;k+=2)
            {
                var a=P(0);var b=P(k);var c=P(k+1);var d=k+2<n?P(k+2):c;
                w.Quads.Add(new RooftopQuad{A=a,B=b,C=c,D=d,Normal=under?-Vector3.UnitY:Vector3.UnitY,Uv=new Vector4(a.X,a.Z,c.X,c.Z),Mode=mode,Attr=attr});
            }
        }
        // A prism: the footprint's walls from y0 to y1 (in `mode`) and its top.
        public static void Extrude(World w,Vector3[] fp,float y0,float y1,int mode,Vector4 attr,Vector4 cap,float top=0,int capMode=Prop)
        {
            for(int k=0;k<fp.Length;k++)Wall(w,fp[k],fp[(k+1)%fp.Length],y0,y1,mode,attr,top);
            Cap(w,fp,y1,capMode,cap);
        }
        // The footprint grown (or shrunk, negative) by `d` along each edge's outward normal (convex, so the corners meet).
        private static Vector3[] Grow(Vector3[] fp,float d)
        {
            int n=fp.Length;var o=new Vector3[n];
            for(int k=0;k<n;k++)
            {
                var a=fp[(k+n-1)%n];var b=fp[k];var c=fp[(k+1)%n];
                var n0=Vector3.Normalize(Vector3.Cross(new Vector3(b.X-a.X,0,b.Z-a.Z),Vector3.UnitY));var n1=Vector3.Normalize(Vector3.Cross(new Vector3(c.X-b.X,0,c.Z-b.Z),Vector3.UnitY));
                var m=Vector3.Normalize(n0+n1);float cos=Vector3.Dot(m,n0);o[k]=b+m*(d/Math.Max(.3f,cos));
            }
            return o;
        }
        // A parapet round a footprint: its outer face, its inner face and the coping between them.
        private static void Parapet(World w,Vector3[] fp,float y,float h,float thick,Vector4 outer,Vector4 inner,Vector4 coping)
        {
            var inside=Grow(fp,-thick);int n=fp.Length;
            for(int k=0;k<n;k++)
            {
                var a=fp[k];var b=fp[(k+1)%n];var ia=inside[k];var ib=inside[(k+1)%n];
                Wall(w,a,b,y,y+h,Prop,outer);Wall(w,ib,ia,y,y+h,Prop,inner);
                var A=new Vector3(a.X,y+h,a.Z);var B=new Vector3(b.X,y+h,b.Z);var C=new Vector3(ib.X,y+h,ib.Z);var D=new Vector3(ia.X,y+h,ia.Z);
                w.Quads.Add(new RooftopQuad{A=A,B=B,C=C,D=D,Normal=Vector3.UnitY,Uv=new Vector4(0,0,Vector3.Distance(A,B),thick),Mode=Prop,Attr=coping});
            }
        }
        // A box with its bottom (Box leaves it off: a ledge or a balcony seen from below).
        private static void Slab(World w,float x0,float y0,float z0,float x1,float y1,float z1,Vector4 attr)
        {
            Box(w,x0,y0,z0,x1,y1,z1,Prop,attr);
            Quad(w,new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),new Vector3(x1,y0,z1),new Vector3(x0,y0,z1),-Vector3.UnitY,new Vector4(x0,z0,x1,z1),Prop,attr);
        }
        // A thin member from a to b (a mast's leg, a brace, a pipe, a cable), square in section `r` wide, drawn as four long faces.
        public static void Strut(World w,Vector3 a,Vector3 b,float r,Vector4 attr)
        {
            var d=b-a;float len=d.Length();if(len<1e-4f)return;d/=len;var side=Math.Abs(d.Y)>.9f?Vector3.UnitX:Vector3.UnitY;
            var u=Vector3.Normalize(Vector3.Cross(d,side))*(r*.5f);var v=Vector3.Normalize(Vector3.Cross(d,u))*(r*.5f);
            var corners=new[]{u+v,-u+v,-u-v,u-v};
            for(int k=0;k<4;k++)
            {
                var c0=corners[k];var c1=corners[(k+1)%4];var n=Vector3.Normalize(c0+c1);
                w.Quads.Add(new RooftopQuad{A=a+c0,B=a+c1,C=b+c1,D=b+c0,Normal=n,Uv=new Vector4(0,0,r,len),Mode=Prop,Attr=attr});
            }
        }
        private static bool Sees(Vector3 eye,Vector3 at,Vector3 n)=>Vector3.Dot(n,new Vector3(eye.X-at.X,0,eye.Z-at.Z))>0;

        // ---- the towers ----
        private static void Towers(World w,List<Lot> lots)
        {
            Camera(0,out var eye,out var fwd);var ahead=Vector3.Normalize(new Vector3(fwd.X,0,fwd.Z));
            var trims=new List<(Vector3[] fp,float y,float hue,float seed)>();
            foreach(var l in lots)
            {
                float x0=l.X0,x1=l.X0+l.W,z0=l.Z0-l.D,z1=l.Z0,top=Street+l.H;
                var mid=new Vector3((x0+x1)*.5f,0,(z0+z1)*.5f);var to=new Vector3(mid.X-eye.X,0,mid.Z-eye.Z);float dist=to.Length();float facing=dist>1?Vector3.Dot(to/dist,ahead):1;
                int detail=facing<ViewFacing?-1:dist<NearDetail?2:dist<MidDetail?1:0;
                var r=new Random((uint)Math.Floor(l.Seed*131)+7);float pick=r.Next(),pick2=r.Next();
                int style=l.H>170?(pick<.55f?Glazed:Corporate):l.H>95?(pick<.35f?Glazed:pick<.7f?Corporate:Residential):(pick<.45f?Residential:pick<.8f?Masonry:Glazed);
                // the seed whole (the facade shader rounds it back from the interpolator before hashing it)
                float share=.14f+.42f*r.Next(),seed=(float)Math.Floor(l.Seed);var attr=new Vector4(seed,style,l.Hue,share);
                float chamfer=style==Glazed&&pick2<.6f?Lerp(2.5f,6,r.Next()):style==Corporate&&pick2<.3f?Lerp(1.5f,3,r.Next()):0,baseChamfer=chamfer,firstTop=top;
                int tiers=style==Corporate?2+(int)Math.Floor(r.Next()*2.99f):style==Glazed?(l.Tier>.6f?2:1):style==Residential?(l.Tier>.75f?2:1):1;
                if(l.Bare)tiers=1;
                float y=Street;Vector3[] fp=null;
                for(int k=0;k<tiers;k++)
                {
                    float ty=k==tiers-1?top:Lerp(Street,top,(k+1)/(tiers+.45f));fp=Footprint(x0,z0,x1,z1,chamfer);
                    Extrude(w,fp,y,ty,Tower,attr,RoofSkin,ty);if(k==0)firstTop=ty;
                    if(k<tiers-1)
                    {
                        if(detail>=1){var ledge=Grow(fp,.7f);for(int e=0;e<ledge.Length;e++)Wall(w,ledge[e],ledge[(e+1)%ledge.Length],ty-.55f,ty+.12f,Prop,PaleConcrete);Cap(w,ledge,ty+.12f,Prop,PaleConcrete);Cap(w,ledge,ty-.55f,Prop,DarkConcrete,true);}
                        if(l.Tier>.8f||style==Corporate)trims.Add((fp,ty,l.Hue,l.Seed+k));
                    }
                    y=ty;float ins=Math.Min(5,(x1-x0)*.12f);x0+=ins;x1-=ins;z0+=ins;z1-=ins;chamfer=Math.Max(0,chamfer-ins*.3f);
                }
                trims.Add((fp,top,l.Hue,l.Seed+9));
                x0=l.X0;x1=l.X0+l.W;z0=l.Z0-l.D;z1=l.Z0;
                if(l.Bare||detail<0)continue;
                var rt=new Random((uint)Math.Floor(l.Seed*977)+3);
                // the top: a parapet, the plant, and the style's crown
                var topFp=fp;float tx0=topFp[0].X,tx1=topFp[0].X,tz0=topFp[0].Z,tz1=topFp[0].Z;foreach(var p in topFp){tx0=Math.Min(tx0,p.X);tx1=Math.Max(tx1,p.X);tz0=Math.Min(tz0,p.Z);tz1=Math.Max(tz1,p.Z);}
                if(detail>=1)Parapet(w,topFp,top,1.1f,.35f,DarkConcrete,DarkConcrete,PaleConcrete);
                var tc=new Vector3((tx0+tx1)*.5f,top,(tz0+tz1)*.5f);float tw=tx1-tx0,td=tz1-tz0;
                if(detail>=1)Plant(w,rt,tx0+2,tz0+2,tx1-2,tz1-2,top,detail);
                if(style==Glazed)
                {
                    // a glazed lantern on the roof and a mast with a blinking beacon
                    float lw=tw*.45f,ld=td*.45f;var lan=Footprint(tc.X-lw/2,tc.Z-ld/2,tc.X+lw/2,tc.Z+ld/2,Math.Min(2,lw*.2f));
                    Extrude(w,lan,top,top+5.5f,Tower,new Vector4(seed+3,Glazed,l.Hue,Math.Min(1,share+.3f)),DarkSteel,top+5.5f);
                    if(r.Next()<.8f)Mast(w,rt,new Vector3(tc.X,top+5.5f,tc.Z),12+18*r.Next(),detail);
                    if(detail>=1&&chamfer<=0)Fins(w,topFp,Street,top,Steel);
                }
                else if(style==Corporate)
                {
                    if(rt.Next()<.45f){if(detail>=1)Helipad(w,tc,Math.Min(tw,td)*.38f,top+.2f);}
                    else if(detail>=1||l.H>200)Crown(w,topFp,top,l.Hue,rt);
                    if(detail>=1)CornerNeon(w,topFp,Street+30,top,l.Hue);
                    if(rt.Next()<.7f)Mast(w,rt,new Vector3(tc.X+tw*.3f,top,tc.Z-td*.3f),8+14*rt.Next(),detail);
                }
                else
                {
                    if(rt.Next()<.65f&&detail>=1)Tank(w,rt,new Vector3(Lerp(tx0+4,tx1-4,rt.Next()),top,Lerp(tz0+4,tz1-4,rt.Next())));
                    if(detail>=1&&rt.Next()<.55f)Billboard(w,rt,eye,topFp,top,l.Hue);
                    if(style==Masonry&&detail>=1)Cornice(w,topFp,top);
                }
                // the faces the camera sees, near: balconies, air-con, a fire escape, a hanging sign
                var baseFp=Footprint(x0,z0,x1,z1,baseChamfer);
                if(detail==2)for(int e=0;e<baseFp.Length;e++)
                {
                    var p0=baseFp[e];var p1=baseFp[(e+1)%baseFp.Length];
                    var n=Vector3.Normalize(Vector3.Cross(new Vector3(p1.X-p0.X,0,p1.Z-p0.Z),Vector3.UnitY));
                    if(!Sees(eye,(p0+p1)*.5f,n)||Vector3.Distance(p0,p1)<10||Math.Max(Math.Abs(n.X),Math.Abs(n.Z))<.99f)continue;
                    float bot=Street+8;
                    if(style==Residential)Balconies(w,rt,p0,p1,n,bot,firstTop-4,3.0f);
                    if(style==Residential||style==Masonry)AirCon(w,rt,p0,p1,n,bot,firstTop-3,style==Masonry?3.3f:3.0f);
                    if(style==Masonry&&rt.Next()<.7f)FireEscape(w,rt,p0,p1,n,bot,firstTop-2);
                    if(rt.Next()<.45f)HangingSign(w,rt,p0,p1,n,Math.Max(Street+12,firstTop-30-rt.Next()*20),l.Hue);
                }
            }
            // neon roof and setback trims (about two of three), the roof's glow over some
            var tr=new Random(5);
            foreach(var t in trims)
            {
                if(tr.Next()<.35f)continue;var col=t.hue<.55f?new Vector3(.2f,2.4f,3f):new Vector3(2.8f,.25f,2.2f);col*=.8f+tr.Next()*.8f;
                for(int k=0;k<t.fp.Length;k++){var a=t.fp[k];var b=t.fp[(k+1)%t.fp.Length];NeonBar(w,new Vector3(a.X,t.y,a.Z),new Vector3(b.X,t.y,b.Z),.35f,col);}
                if(Hash2((int)Math.Floor(t.seed),9)>=.65f){var c=Vector3.Zero;foreach(var p in t.fp)c+=p;c/=t.fp.Length;w.Glows.Add(new RooftopSprite{Position=new Vector3(c.X,t.y,c.Z),Color=t.hue<.55f?new Vector4(.15f,.7f,.9f,.18f):new Vector4(.8f,.1f,.6f,.18f),Size=40,Kind=Glow});}
            }
        }
        // Roof plant: air handlers with their grilles, smaller condensers, a duct run, a stair house.
        private static void Plant(World w,Random r,float x0,float z0,float x1,float z1,float y,int detail)
        {
            if(x1-x0<6||z1-z0<6)return;int n=detail>=2?3+(int)(r.Next()*4):1+(int)(r.Next()*2);
            for(int k=0;k<n;k++)
            {
                float bw=2+r.Next()*4.5f,bd=1.6f+r.Next()*3,bh=1.2f+r.Next()*2.2f,cx=Lerp(x0+bw,x1-bw,r.Next()),cz=Lerp(z0+bd,z1-bd,r.Next());
                Box(w,cx-bw/2,y,cz-bd/2,cx+bw/2,y+bh,cz+bd/2,Prop,r.Next()<.5f?Steel:DarkSteel);
                Box(w,cx-bw/2+.2f,y+bh,cz-bd/2+.2f,cx+bw/2-.2f,y+bh+.12f,cz+bd/2-.2f,Prop,Vent);
                if(detail>=2&&r.Next()<.5f)Box(w,cx+bw/2,y+.3f,cz-.25f,cx+bw/2+2.5f,y+.8f,cz+.25f,Prop,DarkSteel);
            }
            if(r.Next()<.5f){float hx=Lerp(x0,x1-4,r.Next()),hz=Lerp(z0,z1-3,r.Next());Box(w,hx,y,hz,hx+3.6f,y+3,hz+2.6f,Prop,DarkConcrete);Box(w,hx-.2f,y+3,hz-.2f,hx+3.8f,y+3.18f,hz+2.8f,Prop,PaleConcrete);}
        }
        // A lattice mast: three legs tapering to a pole, rings of braces, a red beacon blinking at the top; far off (`detail` 0,
        // its members under a pixel) a single pole under the beacon.
        private static void Mast(World w,Random r,Vector3 at,float h,int detail=2)
        {
            if(detail<1){Strut(w,at,at+new Vector3(0,h,0),.6f,DarkSteel);w.Glows.Add(new RooftopSprite{Position=at+new Vector3(0,h+.2f,0),Color=new Vector4(2.6f,.15f,.25f,1),Size=3.2f,Kind=Beacon,Seed=r.Next()});return;}
            float b=.9f;var legs=new[]{at+new Vector3(b,0,0),at+new Vector3(-b*.5f,0,b*.87f),at+new Vector3(-b*.5f,0,-b*.87f)};var tip=at+new Vector3(0,h*.75f,0);
            foreach(var p in legs)Strut(w,p,tip,.09f,DarkSteel);
            for(int k=1;k<5;k++){float f=k/5f;for(int i=0;i<3;i++)Strut(w,Vector3.Lerp(legs[i],tip,f),Vector3.Lerp(legs[(i+1)%3],tip,f),.05f,DarkSteel);}
            Strut(w,tip,at+new Vector3(0,h,0),.07f,Steel);
            w.Glows.Add(new RooftopSprite{Position=at+new Vector3(0,h+.2f,0),Color=new Vector4(2.6f,.15f,.25f,1),Size=2.2f,Kind=Beacon,Seed=r.Next()});
        }
        // Vertical metal fins up a rectangular glazed tower's corners and every third of its faces.
        private static void Fins(World w,Vector3[] fp,float y0,float y1,Vector4 attr)
        {
            if(fp.Length!=4)return;
            for(int k=0;k<4;k++)
            {
                var a=fp[k];var b=fp[(k+1)%4];var n=Vector3.Normalize(Vector3.Cross(new Vector3(b.X-a.X,0,b.Z-a.Z),Vector3.UnitY));
                for(int f=0;f<=3;f++){var p=Vector3.Lerp(a,b,f/3f)+n*.35f;var t=Vector3.Normalize(b-a)*.2f;Box(w,Math.Min(p.X-t.X,p.X+t.X)-.12f,y0,Math.Min(p.Z-t.Z,p.Z+t.Z)-.12f,Math.Max(p.X-t.X,p.X+t.X)+.12f,y1+.8f,Math.Max(p.Z-t.Z,p.Z+t.Z)+.12f,Prop,attr);}
            }
        }
        // A corporate crown: a lit ring frame on posts above the roof.
        private static void Crown(World w,Vector3[] fp,float y,float hue,Random r)
        {
            var ring=Grow(fp,-1.5f);float h=4+r.Next()*4;var col=hue<.55f?new Vector3(.25f,2.2f,2.9f):new Vector3(2.6f,.3f,2.1f);
            for(int k=0;k<ring.Length;k++)
            {
                var a=ring[k];var b=ring[(k+1)%ring.Length];Strut(w,new Vector3(a.X,y,a.Z),new Vector3(a.X,y+h,a.Z),.3f,DarkSteel);
                Strut(w,new Vector3(a.X,y+h,a.Z),new Vector3(b.X,y+h,b.Z),.35f,DarkSteel);NeonBar(w,new Vector3(a.X,y+h-.2f,a.Z),new Vector3(b.X,y+h-.2f,b.Z),.25f,col);
            }
        }
        // A helipad: a pale deck, its edge lights and the H in neon.
        private static void Helipad(World w,Vector3 c,float r,float y)
        {
            var deck=Ring(c,r,12,.13f);Extrude(w,deck,y-.4f,y,Prop,DarkSteel,PaleConcrete);
            var col=new Vector3(2.4f,1.9f,.6f);
            for(int k=0;k<12;k+=2){var p=deck[k];w.Glows.Add(new RooftopSprite{Position=new Vector3(p.X,y+.15f,p.Z),Color=new Vector4(1.8f,1.5f,.6f,.9f),Size=1.2f,Kind=Glow});}
            float s=r*.35f;Quad(w,new Vector3(c.X-s,y+.02f,c.Z+s),new Vector3(c.X-s+.6f,y+.02f,c.Z+s),new Vector3(c.X-s+.6f,y+.02f,c.Z-s),new Vector3(c.X-s,y+.02f,c.Z-s),Vector3.UnitY,Vector4.Zero,Neon,new Vector4(col,1));
            Quad(w,new Vector3(c.X+s-.6f,y+.02f,c.Z+s),new Vector3(c.X+s,y+.02f,c.Z+s),new Vector3(c.X+s,y+.02f,c.Z-s),new Vector3(c.X+s-.6f,y+.02f,c.Z-s),Vector3.UnitY,Vector4.Zero,Neon,new Vector4(col,1));
            Quad(w,new Vector3(c.X-s,y+.02f,c.Z+.3f),new Vector3(c.X+s,y+.02f,c.Z+.3f),new Vector3(c.X+s,y+.02f,c.Z-.3f),new Vector3(c.X-s,y+.02f,c.Z-.3f),Vector3.UnitY,Vector4.Zero,Neon,new Vector4(col,1));
        }
        // Neon up a tower's corners from `y0` to its top.
        private static void CornerNeon(World w,Vector3[] fp,float y0,float y1,float hue)
        {
            var col=hue<.55f?new Vector3(.2f,2.0f,2.6f):new Vector3(2.4f,.25f,1.9f);
            for(int k=0;k<fp.Length;k+=fp.Length==4?1:2)
            {
                var p=fp[k];var prev=fp[(k+fp.Length-1)%fp.Length];var next=fp[(k+1)%fp.Length];
                var n=Vector3.Normalize(Vector3.Normalize(Vector3.Cross(new Vector3(p.X-prev.X,0,p.Z-prev.Z),Vector3.UnitY))+Vector3.Normalize(Vector3.Cross(new Vector3(next.X-p.X,0,next.Z-p.Z),Vector3.UnitY)));
                var t=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,n))*.2f;var o=p+n*.06f;
                Quad(w,new Vector3(o.X-t.X,y0,o.Z-t.Z),new Vector3(o.X+t.X,y0,o.Z+t.Z),new Vector3(o.X+t.X,y1,o.Z+t.Z),new Vector3(o.X-t.X,y1,o.Z-t.Z),n,Vector4.Zero,Neon,new Vector4(col*.7f,1));
            }
        }
        // A water tank on its stand: four legs and a ring beam, the staves, a conical lid.
        private static void Tank(World w,Random r,Vector3 at,float scale=1)
        {
            float rad=(1.6f+r.Next()*.9f)*scale,leg=(2.2f+r.Next()*1.2f)*scale,h=(2.8f+r.Next()*1.4f)*scale;var wood=r.Next()<.6f?Mat(.075f,.048f,.032f,Metal):Steel;
            for(int k=0;k<4;k++){float a=k*(float)Math.PI/2+.785f;var p=at+new Vector3(rad*.8f*(float)Math.Cos(a),0,rad*.8f*(float)Math.Sin(a));Strut(w,p,p+new Vector3(0,leg,0),.18f,DarkSteel);}
            var fp=Ring(new Vector3(at.X,0,at.Z),rad,10);Cap(w,fp,at.Y+leg,Prop,DarkSteel,true);
            for(int k=0;k<fp.Length;k++)Wall(w,fp[k],fp[(k+1)%fp.Length],at.Y+leg,at.Y+leg+h,Prop,wood);
            for(int b=1;b<=2;b++){var band=Grow(fp,.04f);float by=at.Y+leg+h*b/3f;for(int k=0;k<band.Length;k++)Wall(w,band[k],band[(k+1)%band.Length],by-.06f,by+.06f,Prop,DarkSteel);}
            var apex=new Vector3(at.X,at.Y+leg+h+rad*.55f,at.Z);
            for(int k=0;k<fp.Length;k++)
            {
                var a=new Vector3(fp[k].X,at.Y+leg+h,fp[k].Z);var b=new Vector3(fp[(k+1)%fp.Length].X,at.Y+leg+h,fp[(k+1)%fp.Length].Z);var n=Vector3.Normalize(Vector3.Cross(b-a,apex-a));
                w.Quads.Add(new RooftopQuad{A=a,B=b,C=apex,D=apex,Normal=n,Uv=new Vector4(0,0,1,1),Mode=Prop,Attr=DarkSteel});
            }
        }
        // A billboard on posts facing the camera: its frame, a dark back and a lit glyph panel.
        private static void Billboard(World w,Random r,Vector3 eye,Vector3[] fp,float y,float hue)
        {
            // along the roof edge nearest the camera
            int best=0;float bd=float.MaxValue;for(int k=0;k<fp.Length;k++){var m=(fp[k]+fp[(k+1)%fp.Length])*.5f;float d=Vector2.Distance(new Vector2(m.X,m.Z),new Vector2(eye.X,eye.Z));if(d<bd){bd=d;best=k;}}
            var a=fp[best];var b=fp[(best+1)%fp.Length];var n=Vector3.Normalize(Vector3.Cross(new Vector3(b.X-a.X,0,b.Z-a.Z),Vector3.UnitY));float len=Vector3.Distance(a,b);if(len<12)return;
            var t=Vector3.Normalize(b-a);float bw=Math.Min(18,len*.6f),bh=bw*(.45f+.2f*r.Next()),lift=2.5f;var c=Vector3.Lerp(a,b,.5f)-n*2.5f;
            var p0=c-t*bw/2;var p1=c+t*bw/2;
            foreach(var p in new[]{p0,p1,c})Strut(w,new Vector3(p.X,y,p.Z),new Vector3(p.X,y+lift+bh,p.Z),.35f,DarkSteel);
            Wall(w,p1,p0,y+lift,y+lift+bh,Prop,DarkSteel);
            var f0=p0+n*.25f;var f1=p1+n*.25f;var glow=hue<.5f?new Vector3(.3f,1.8f,2.4f):hue<.8f?new Vector3(2.4f,.35f,1.6f):new Vector3(2.2f,1.4f,.4f);
            Wall(w,f0,f1,y+lift+.3f,y+lift+bh-.3f,Prop,new Vector4(glow*(.8f+.4f*r.Next()),SignPanel));
            Strut(w,new Vector3(f0.X,y+lift,f0.Z),new Vector3(f1.X,y+lift,f1.Z),.3f,Steel);Strut(w,new Vector3(f0.X,y+lift+bh,f0.Z),new Vector3(f1.X,y+lift+bh,f1.Z),.3f,Steel);
        }
        // A masonry top: a stepped cornice round the parapet.
        private static void Cornice(World w,Vector3[] fp,float y)
        {
            var c1=Grow(fp,.45f);var c2=Grow(fp,.8f);
            for(int k=0;k<fp.Length;k++){Wall(w,c1[k],c1[(k+1)%fp.Length],y-1.2f,y-.6f,Prop,PaleConcrete);Wall(w,c2[k],c2[(k+1)%fp.Length],y-.6f,y-.25f,Prop,PaleConcrete);}
            Cap(w,c1,y-1.2f,Prop,DarkConcrete,true);Cap(w,c2,y-.6f,Prop,DarkConcrete,true);
        }
        // Balconies down a face: slabs at every other storey in bays of three, a rail on each, a few lit by a lamp.
        private static void Balconies(World w,Random r,Vector3 a,Vector3 b,Vector3 n,float y0,float y1,float storey)
        {
            var t=Vector3.Normalize(b-a);float len=Vector3.Distance(a,b);float bay=2.6f*3;int count=(int)Math.Floor((len-2)/bay);if(count<1)return;
            for(float y=y0+storey;y<y1;y+=storey*2)for(int k=0;k<count;k++)
            {
                if(Hash2((int)(y*7),k*13+(int)(a.X))<.3f)continue;
                var c0=a+t*(1+k*bay+.4f);var c1=a+t*(1+(k+1)*bay-.4f);var o0=c0+n*1.2f;var o1=c1+n*1.2f;
                float xa=Math.Min(Math.Min(c0.X,c1.X),Math.Min(o0.X,o1.X)),xb=Math.Max(Math.Max(c0.X,c1.X),Math.Max(o0.X,o1.X)),za=Math.Min(Math.Min(c0.Z,c1.Z),Math.Min(o0.Z,o1.Z)),zb=Math.Max(Math.Max(c0.Z,c1.Z),Math.Max(o0.Z,o1.Z));
                Slab(w,xa,y-.18f,za,xb,y,zb,DarkConcrete);
                Wall(w,o0,o1,y,y+1.05f,Prop,DarkSteel);Strut(w,new Vector3(o0.X,y+1.05f,o0.Z),new Vector3(o1.X,y+1.05f,o1.Z),.06f,Steel);
            }
        }
        // Air-con boxes under some windows of a face.
        private static void AirCon(World w,Random r,Vector3 a,Vector3 b,Vector3 n,float y0,float y1,float storey)
        {
            var t=Vector3.Normalize(b-a);float len=Vector3.Distance(a,b);
            for(float y=y0+storey*.25f;y<y1;y+=storey)for(float s=1.2f;s<len-1.2f;s+=2.6f)
            {
                if(Hash2((int)(y*3.1f),(int)(s*5.3f)+(int)a.Z)>.16f)continue;
                var p=a+t*s;var q=p+n*.55f;var e=p+t*.9f;var eq=e+n*.55f;
                float xa=Math.Min(Math.Min(p.X,q.X),Math.Min(e.X,eq.X)),xb=Math.Max(Math.Max(p.X,q.X),Math.Max(e.X,eq.X)),za=Math.Min(Math.Min(p.Z,q.Z),Math.Min(e.Z,eq.Z)),zb=Math.Max(Math.Max(p.Z,q.Z),Math.Max(e.Z,eq.Z));
                Box(w,xa,y,za,xb,y+.6f,zb,Prop,Steel);
            }
        }
        // A fire escape: a landing at every storey on brackets, rails, the stair flights zigzagging between them.
        private static void FireEscape(World w,Random r,Vector3 a,Vector3 b,Vector3 n,float y0,float y1)
        {
            var t=Vector3.Normalize(b-a);float len=Vector3.Distance(a,b);if(len<14)return;float s0=len*(.2f+.4f*r.Next());var c0=a+t*s0;var c1=c0+t*5.5f;const float storey=3.3f;bool flip=false;
            for(float y=y0;y<y1;y+=storey)
            {
                var o0=c0+n*1.3f;var o1=c1+n*1.3f;
                float xa=Math.Min(Math.Min(c0.X,c1.X),Math.Min(o0.X,o1.X)),xb=Math.Max(Math.Max(c0.X,c1.X),Math.Max(o0.X,o1.X)),za=Math.Min(Math.Min(c0.Z,c1.Z),Math.Min(o0.Z,o1.Z)),zb=Math.Max(Math.Max(c0.Z,c1.Z),Math.Max(o0.Z,o1.Z));
                Slab(w,xa,y-.08f,za,xb,y,zb,Rust);Strut(w,new Vector3(o0.X,y+1,o0.Z),new Vector3(o1.X,y+1,o1.Z),.05f,Rust);
                if(y+storey<y1){var s0p=(flip?c1:c0)+n*.65f;var s1p=(flip?c0:c1)+n*.65f;Strut(w,new Vector3(s0p.X,y,s0p.Z),new Vector3((s0p.X+s1p.X)*.5f,y+storey,(s0p.Z+s1p.Z)*.5f),.12f,Rust);}
                flip=!flip;
            }
        }
        // A vertical sign hanging off a face on brackets: its box and both lit glyph faces.
        private static void HangingSign(World w,Random r,Vector3 a,Vector3 b,Vector3 n,float y,float hue)
        {
            var t=Vector3.Normalize(b-a);float len=Vector3.Distance(a,b);var at=a+t*(len*(.08f+.15f*r.Next()));float h=7+r.Next()*9,sw=1.2f,dep=1.8f;
            var p0=at+n*.6f;var p1=at+n*(.6f+dep);var glow=r.Next()<.45f?new Vector3(2.4f,.3f,1.7f):r.Next()<.6f?new Vector3(.3f,1.9f,2.5f):new Vector3(2.4f,1.5f,.4f);
            var s=t*(sw*.5f);
            float xa=Math.Min(Math.Min(p0.X-s.X,p0.X+s.X),Math.Min(p1.X-s.X,p1.X+s.X)),xb=Math.Max(Math.Max(p0.X-s.X,p0.X+s.X),Math.Max(p1.X-s.X,p1.X+s.X)),za=Math.Min(Math.Min(p0.Z-s.Z,p0.Z+s.Z),Math.Min(p1.Z-s.Z,p1.Z+s.Z)),zb=Math.Max(Math.Max(p0.Z-s.Z,p0.Z+s.Z),Math.Max(p1.Z-s.Z,p1.Z+s.Z));
            Box(w,xa,y,za,xb,y+h,zb,Prop,DarkSteel);
            // the two faces across the street (either side of the sign)
            Wall(w,p1+s+t*.01f,p0+s+t*.01f,y+.25f,y+h-.25f,Prop,new Vector4(glow,SignPanel));Wall(w,p0-s-t*.01f,p1-s-t*.01f,y+.25f,y+h-.25f,Prop,new Vector4(glow*.9f,SignPanel));
            Strut(w,at+new Vector3(0,y+h-.4f,0),at+n*.6f+new Vector3(0,y+h-.4f,0),.12f,DarkSteel);
            w.Glows.Add(new RooftopSprite{Position=(p0+p1)*.5f+new Vector3(0,y+h*.5f,0),Color=new Vector4(glow*.25f,.25f),Size=h*1.4f,Kind=Glow});
        }

        // ---- our roof ----
        private static void Perch(World w)
        {
            // the membrane, its laps and wet patches in the shader (one sheet, no tiles), and the roof's thickness
            Quad(w,new Vector3(RoofX0,0,RoofZ1),new Vector3(RoofX1,0,RoofZ1),new Vector3(RoofX1,0,RoofZ0),new Vector3(RoofX0,0,RoofZ0),Vector3.UnitY,new Vector4(RoofX0,RoofZ0,RoofX1,RoofZ1),Prop,RoofSkin);
            // our tower below the lip (its windows, the storey ledges)
            var tower=new Vector4(41,Corporate,.12f,.55f);
            Wall(w,new Vector3(RoofX1,0,RoofZ0),new Vector3(RoofX0,0,RoofZ0),Street,-.4f,Tower,tower,0);Wall(w,new Vector3(RoofX1,0,RoofZ1),new Vector3(RoofX1,0,RoofZ0),Street,-.4f,Tower,tower,0);
            Wall(w,new Vector3(RoofX1,0,RoofZ0),new Vector3(RoofX0,0,RoofZ0),-.4f,0,Prop,DarkConcrete);Wall(w,new Vector3(RoofX1,0,RoofZ1),new Vector3(RoofX1,0,RoofZ0),-.4f,0,Prop,DarkConcrete);
            for(float y=-4;y>-60;y-=3.6f){Slab(w,RoofX0,y-.25f,RoofZ0-.35f,RoofX1+.35f,y,RoofZ0,DarkConcrete);Slab(w,RoofX1,y-.25f,RoofZ0,RoofX1+.35f,y,RoofZ1,DarkConcrete);}
            // the lip: its block, a coping that overhangs, the cyan strip under the coping on the outer face
            Box(w,RoofX0,0,RoofZ0,RoofX1,LipTop-.06f,RoofZ0+LipDepth,Prop,DarkConcrete);Box(w,RoofX1-LipDepth,0,RoofZ0+LipDepth,RoofX1,LipTop-.06f,RoofZ1,Prop,DarkConcrete);
            Box(w,RoofX0,LipTop-.06f,RoofZ0-.05f,RoofX1+.05f,LipTop,RoofZ0+LipDepth+.03f,Prop,PaleConcrete);Box(w,RoofX1-LipDepth-.03f,LipTop-.06f,RoofZ0+LipDepth+.03f,RoofX1+.05f,LipTop,RoofZ1,Prop,PaleConcrete);
            NeonBar(w,new Vector3(RoofX1,LipTop-.08f,RoofZ0-.06f),new Vector3(RoofX0,LipTop-.08f,RoofZ0-.06f),.035f,new Vector3(.15f,1.6f,2f));
            NeonBar(w,new Vector3(RoofX1+.06f,LipTop-.08f,RoofZ1),new Vector3(RoofX1+.06f,LipTop-.08f,RoofZ0),.035f,new Vector3(.15f,1.6f,2f));
            // the HVAC unit where the old box stood: its casing, louvred sides, the fan grille on top in its ring, feet
            Box(w,-3.9f,.12f,-1.25f,-2.1f,1.15f,-.05f,Prop,Steel);for(int k=0;k<2;k++)Box(w,-3.85f+k*1.45f,0,-1.2f,-3.55f+k*1.45f,.12f,-.1f,Prop,DarkSteel);
            Wall(w,new Vector3(-3.85f,0,-.04f),new Vector3(-2.15f,0,-.04f),.25f,1.0f,Prop,Vent);Wall(w,new Vector3(-2.09f,0,-.15f),new Vector3(-2.09f,0,-1.15f),.35f,.95f,Prop,Vent);
            var fan=Ring(new Vector3(-3f,0,-.65f),.48f,12);Extrude(w,Grow(fan,.08f),1.15f,1.27f,Prop,DarkSteel,DarkSteel);Cap(w,fan,1.275f,Prop,Vent);
            Strut(w,new Vector3(-2.1f,.6f,-.4f),new Vector3(-1.4f,.6f,-.4f),.12f,DarkSteel);Strut(w,new Vector3(-1.4f,.6f,-.4f),new Vector3(-1.4f,.02f,-.4f),.12f,DarkSteel);
            Quad(w,new Vector3(-2.08f,1.0f,-.2f),new Vector3(-2.08f,1.0f,-.3f),new Vector3(-2.08f,1.08f,-.3f),new Vector3(-2.08f,1.08f,-.2f),Vector3.UnitX,Vector4.Zero,Neon,new Vector4(.2f,2.4f,.6f,1));
            // the vent stack and its cowl
            var stack=Ring(new Vector3(-4.9f,0,-.6f),.3f,10);Extrude(w,stack,0,1.6f,Prop,Steel,DarkSteel);Extrude(w,Grow(stack,.12f),1.6f,1.9f,Prop,DarkSteel,DarkSteel);
            // the lattice mast with its dish, on a footing, the beacon at its top
            Box(w,-7.5f,0,-1.7f,-6.4f,.25f,-.6f,Prop,DarkConcrete);
            var mb=new Vector3(-6.95f,.25f,-1.15f);var legs=new[]{mb+new Vector3(.32f,0,0),mb+new Vector3(-.16f,0,.28f),mb+new Vector3(-.16f,0,-.28f)};var mtop=new Vector3(-6.95f,8.6f,-1.15f);
            foreach(var p in legs)Strut(w,p,mtop,.06f,DarkSteel);
            for(int k=1;k<9;k++){float f=k/9f;for(int i=0;i<3;i++)Strut(w,Vector3.Lerp(legs[i],mtop,f),Vector3.Lerp(legs[(i+1)%3],mtop,f*1.04f),.03f,DarkSteel);}
            Strut(w,mtop,new Vector3(-6.95f,9.1f,-1.15f),.05f,Steel);
            var dish=new Vector3(-6.6f,6.2f,-1.3f);Strut(w,new Vector3(-6.9f,6.1f,-1.15f),dish,.05f,Steel);
            var rim=Ring(new Vector3(dish.X+.05f,0,dish.Z),.42f,10);for(int k=0;k<rim.Length;k++){var a=rim[k];var b=rim[(k+1)%rim.Length];Strut(w,new Vector3(a.X,dish.Y+(a.Z-dish.Z)*.6f,a.Z),new Vector3(b.X,dish.Y+(b.Z-dish.Z)*.6f,b.Z),.04f,Steel);}
            // the stair house: walls, a roof that overhangs, a door in its lit frame, a lamp over it, a vent and a sign
            Box(w,-24,0,4,-15,4.2f,13,Prop,DarkConcrete);Slab(w,-24.4f,4.2f,3.6f,-14.6f,4.45f,13.4f,PaleConcrete);
            Wall(w,new Vector3(-14.98f,0,8.4f),new Vector3(-14.98f,0,7.0f),0,2.3f,Prop,DarkSteel);
            NeonBar(w,new Vector3(-14.97f,2.42f,8.5f),new Vector3(-14.97f,2.42f,6.9f),.06f,new Vector3(1.6f,1.1f,.6f));
            Box(w,-15,2.6f,7.5f,-14.6f,2.8f,7.9f,Prop,Mat(1.8f,1.2f,.7f,Lamp));
            w.Glows.Add(new RooftopSprite{Position=new Vector3(-14.7f,2.55f,7.7f),Color=new Vector4(1.4f,.9f,.5f,.6f),Size=3.2f,Kind=Glow});
            Box(w,-17.2f,4.45f,6,-16,5.4f,7.4f,Prop,Steel);Box(w,-17.1f,5.4f,6.1f,-16.1f,5.5f,7.3f,Prop,Vent);
            Wall(w,new Vector3(-14.99f,0,12),new Vector3(-14.99f,0,9),1.2f,3.3f,Prop,Mat(.4f,1.9f,2.4f,SignPanel));
            // a water tank on its stand behind the plant
            Tank(w,new Random(13),new Vector3(-11.6f,0,.9f),.72f);
            // pipes on sleepers from the plant to the stair house, a cable tray along the lip
            for(float x=-13.5f;x<-4.5f;x+=1.6f)Box(w,x,0,.25f,x+.3f,.18f,.85f,Prop,DarkConcrete);
            Strut(w,new Vector3(-13.5f,.32f,.42f),new Vector3(-4.2f,.32f,.42f),.16f,Steel);Strut(w,new Vector3(-13.5f,.32f,.68f),new Vector3(-4.2f,.32f,.68f),.1f,Rust);
            Strut(w,new Vector3(-4.2f,.32f,.42f),new Vector3(-3.6f,.32f,-.05f),.16f,Steel);
            Strut(w,new Vector3(-14,.08f,-1.05f),new Vector3(-1.3f,.08f,-1.05f),.12f,DarkSteel);
            // crates and a tool case by the plant
            Box(w,-6.1f,0,.1f,-5.3f,.75f,.9f,Prop,Mat(.07f,.05f,.035f,Metal));Box(w,-5.95f,.75f,.25f,-5.45f,1.15f,.75f,Prop,Mat(.07f,.05f,.035f,Metal));Box(w,-1.9f,0,.4f,-1.2f,.32f,.75f,Prop,Mat(.11f,.03f,.03f,Metal));
            // a string of lamps from the mast to a pole by the stair house
            var pole=new Vector3(-14.8f,0,-.9f);Strut(w,pole,pole+new Vector3(0,3.4f,0),.08f,DarkSteel);
            var s0=new Vector3(-6.95f,3.6f,-1.15f);var s1=pole+new Vector3(0,3.3f,0);Vector3 prev=s0;
            for(int k=1;k<=12;k++){float f=k/12f;var p=Vector3.Lerp(s0,s1,f)-new Vector3(0,.55f*4*f*(1-f),0);Strut(w,prev,p,.015f,DarkSteel);if(k<12)w.Glows.Add(new RooftopSprite{Position=p-new Vector3(0,.06f,0),Color=new Vector4(1.8f,1.15f,.55f,.95f),Size=.32f,Kind=Glow,Seed=f});prev=p;}
            // puddles
            foreach(var (x,z,s) in new[]{(-1.2f,-.85f,1.3f),(.9f,-.6f,1f),(-4.4f,.6f,1.6f),(-1.6f,1.6f,2.2f),(-8.8f,2.4f,1.8f),(-12.6f,.2f,1.2f),(-3.1f,3.5f,1.4f)}){float hx=s*.8f,hz=s*.5f;Quad(w,new Vector3(x-hx,.012f,z+hz),new Vector3(x+hx,.012f,z+hz),new Vector3(x+hx,.012f,z-hz),new Vector3(x-hx,.012f,z-hz),Vector3.UnitY,new Vector4(0,0,1,1),Puddle,Vector4.Zero);}
            // the blade sign: a magenta box hanging off the corner below the lip on its brackets, its lit letters facing out (+x)
            Box(w,RoofX1+.1f,-7.25f,-2.5f,RoofX1+.6f,-.75f,.1f,Neon,new Vector4(2.4f,.25f,1.9f,1));
            Box(w,RoofX1,-1.15f,-1.6f,RoofX1+.12f,-.95f,-.8f,Prop,DarkSteel);Box(w,RoofX1,-6.9f,-1.6f,RoofX1+.12f,-6.7f,-.8f,Prop,DarkSteel);
            w.Screens.Add(new RooftopScreen{Kind="sign",Center=new Vector3(RoofX1+.62f,-4,-1.2f),Width=2.2f,Height=6,Yaw=(float)Math.PI/2,Gain=2.2f});
            w.SignLight=new Vector3(RoofX1+.4f,-1.2f,-1);w.CyanLight=new Vector3(-1.2f,.9f,-1.2f);w.Beacon=new Vector3(-6.95f,9.2f,-1.15f);
        }

        // A light's colour at `time`: a screen's breathes with its panel (ScreenState: brighter while its own loop plays).
        public static Vector3 ScreenLight(RooftopLight l,float time)
        {
            if(l.Screen==null)return l.Color;ScreenState(l.Screen,time,out float pulse,out float mix,out _,out _);return l.Color*(pulse+.25f*mix);
        }
        // The scene's eight lights: the four holo screens (a little in front of each, reaching about its size), the corner
        // sign, the lip strip, the stair house's lamp and the string of lamps.
        private static void Lights(World w)
        {
            Camera(0,out var eye,out _);
            foreach(var s in w.Screens)
            {
                if(s.Kind=="sign")continue;var toward=Vector3.Normalize(new Vector3(eye.X-s.Center.X,0,eye.Z-s.Center.Z));float size=Math.Max(s.Width,s.Height);
                var col=s.Kind=="siren"?new Vector3(.9f,.3f,1.4f):s.Kind=="chip"?new Vector3(.3f,1.2f,1.5f):s.Kind=="banner"?new Vector3(1.4f,.3f,1.1f):new Vector3(.3f,1.1f,1.5f);
                w.Lights.Add(new RooftopLight{Position=s.Center+toward*size*.35f,Reach=size*1.4f,Wrap=.6f,Color=col*1.8f,Screen=s.Kind});
            }
            w.Lights.Add(new RooftopLight{Position=w.SignLight,Reach=2.6f,Wrap=.3f,Color=new Vector3(2.4f,.25f,1.9f)*.9f,Screen="sign"});
            w.Lights.Add(new RooftopLight{Position=w.CyanLight,Reach=1.6f,Wrap=.2f,Color=new Vector3(.2f,1.2f,1.5f)*.8f});
            w.Lights.Add(new RooftopLight{Position=new Vector3(-14.4f,2.4f,7.7f),Reach=2.8f,Wrap=.2f,Color=new Vector3(1.6f,1.0f,.55f)});
            w.Lights.Add(new RooftopLight{Position=new Vector3(-10.9f,2.6f,-1.0f),Reach=3.8f,Wrap=.4f,Color=new Vector3(1.0f,.62f,.3f)*.7f});
            while(w.Lights.Count>LightCount)w.Lights.RemoveAt(w.Lights.Count-1);
            while(w.Lights.Count<LightCount)w.Lights.Add(new RooftopLight{Position=new Vector3(0,-9999,0),Reach=.01f,Color=Vector3.Zero});
        }
    }
}
