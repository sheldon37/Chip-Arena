using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The home screen's background, "Pilot's Rooftop" (owner 2026-10-12: "have them sitting on top of a building, look down at a
    // cyber city? Can see other pilots in the distance"). Presentation only: a deterministic voxel cyber city built once from a
    // seed, in the source frame (+x the rig's left; the Unity boundary mirrors x, as every Core rule here does). Nothing in it
    // is read by the simulation, the authority or the network frame.
    //  - Our perch: a roof at y 0 with a low lip at its front edge (z -1.8) where the player's pilot sits; its right edge (x 1.7)
    //    drops into the avenue below, so the camera, floating just beyond that corner, looks down the facade.
    //  - The city: 34 m lots on a 50 m street grid with 2-3 voxel setback tiers, rooftop machinery and neon roof trims; one
    //    avenue canyon (x 4..48) runs away under the camera; landmark towers carry the holo screens.
    //  - Everything the composition depends on is placed from screen intent through the loop's t=0 camera at 16:9 (At), so the
    //    duel, the runners' skybridge and the screens land where the concept framed them; generated lots keep out of them.
    //  - Traffic lanes whose periods divide the 48 s loop; the near lane is timed so a car crosses in front of the pilot as its
    //    gaze follows it (NativeRooftopActors).
    public struct RooftopBox {public Vector3 Min,Max;public int Mode;public Vector4 Attr;}
    public struct RooftopQuad {public Vector3 A,B,C,D,Normal;public Vector4 Uv;public int Mode;public Vector4 Attr;}
    public struct RooftopScreen {public string Kind;public Vector3 Center;public float Width,Height,Yaw,Gain;}
    public struct RooftopSite {public string Id;public Vector3 Position;public float Yaw;}
    public struct RooftopSprite {public Vector3 Position;public Vector4 Color;public float Size,Seed;public int Kind;public Vector4 Move;}
    public sealed class RooftopLane {public Vector3[] Points;public int Count;public float Period;public int Dir;public Vector3 Color;public bool Near;public float Phase;}
    public static class NativeRooftopCity
    {
        public const float Loop=48,Street=-140,FovDegrees=42,PlacementAspect=16f/9;
        // Vertex modes the world shader branches on (NativeRooftop.shader): a tower facade (window grid), a lit surface (albedo in
        // Attr), an emissive neon bar (HDR colour in Attr), a puddle (dark mirror, rain rings).
        public const int Facade=0,Lit=1,Neon=2,Puddle=4;
        public const int Kinds=6;
        public const int Glow=0,FlatMist=1,Mote=2,Puff=3,Rain=4,Spark=5;
        // Our roof: x -26..1.7, z -1.8..16 (front edge where the pilot sits), lip .32 high and .42 deep.
        public const float RoofX0=-26,RoofX1=1.7f,RoofZ0=-1.8f,RoofZ1=16,LipTop=.32f,LipDepth=.42f;
        // The seated pilot: centred on the lip, facing the city (-z).
        public static readonly Vector3 PilotSeat=new Vector3(0,0,-1.6f);

        // ---- deterministic noise and random numbers (the concept's own: identical sequences, so the engine matches it) ----
        public static float Hash2(int i,int j){unchecked{int h=i*374761393+j*668265263;h=(int)((uint)(h^(int)((uint)h>>13))*1274126177u);h^=(int)((uint)h>>16);return (uint)h/4294967296f;}}
        public static float Noise(float x,float y){int i=(int)Math.Floor(x),j=(int)Math.Floor(y);float fx=x-i,fy=y-j,u=fx*fx*(3-2*fx),v=fy*fy*(3-2*fy);float a=Hash2(i,j),b=Hash2(i+1,j),c=Hash2(i,j+1),d=Hash2(i+1,j+1);return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v;}
        public static float Fbm(float x,float y,int octaves=4){float s=0,a=.5f,f=1,n=0;for(int k=0;k<octaves;k++){s+=a*Noise(x*f+k*17.3f,y*f-k*9.1f);n+=a;a*=.5f;f*=2.03f;}return s/n;}
        public sealed class Random
        {
            private uint s;public Random(uint seed){s=seed;}
            public float Next(){unchecked{s+=0x6D2B79F5u;uint t=s;t=(t^(t>>15))*(t|1);t^=t+(t^(t>>7))*(t|61);return (t^(t>>14))/4294967296f;}}
        }
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float Clamp(float v,float a,float b)=>v<a?a:v>b?b:v;

        // ---- the camera loop: just beyond our building's right corner, looking back-left at the pilot (seen three-quarter
        // from its right), pitched down over the edge. Every term is periodic in 48 s (or 24 s), so the loop is seamless.
        // `drift` scales the breathing (reduced effects: .5); placement (At) always uses the full loop's t=0 frame. ----
        public static void Camera(float time,out Vector3 position,out Vector3 forward,float drift=1)
        {
            float k=2*(float)Math.PI/Loop;
            position=new Vector3(7.1f+drift*.25f*(float)Math.Sin(k*time),4.45f+drift*.1f*(float)Math.Sin(2*k*time+.7f),4.5f+drift*.25f*(float)Math.Sin(k*time+1.3f));
            float yaw=-.724f+drift*.014f*(float)Math.Sin(k*time+.4f),pitch=-.172f+drift*.01f*(float)Math.Sin(k*time);
            forward=new Vector3((float)(Math.Sin(yaw)*Math.Cos(pitch)),(float)Math.Sin(pitch),(float)(-Math.Cos(yaw)*Math.Cos(pitch)));
        }
        // A world point `dist` metres along the ray through screen point (sx, sy) (0..1, top-left origin) of the t=0 camera at
        // 16:9 (three.js lookAt basis: up +y).
        public static Vector3 At(float sx,float sy,float dist)
        {
            Camera(0,out var eye,out var f);var z=-f;var x=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,z));var y=Vector3.Cross(z,x);
            float th=(float)Math.Tan(FovDegrees*Math.PI/360);var d=Vector3.Normalize(f+x*((sx*2-1)*th*PlacementAspect)+y*(-(sy*2-1)*th));
            return eye+d*dist;
        }

        // ---- the world ----
        public sealed class World
        {
            public readonly List<RooftopQuad> Quads=new List<RooftopQuad>();
            public readonly List<RooftopScreen> Screens=new List<RooftopScreen>();
            public readonly List<RooftopLane> Lanes=new List<RooftopLane>();
            public readonly Dictionary<string,RooftopSite> Sites=new Dictionary<string,RooftopSite>();
            public readonly List<RooftopSprite> Glows=new List<RooftopSprite>(),Mist=new List<RooftopSprite>(),Steam=new List<RooftopSprite>(),Motes=new List<RooftopSprite>(),Rain=new List<RooftopSprite>();
            public Vector3 SparkAt,SignLight,CyanLight,Beacon;public int LotCount;
        }
        private struct Lot {public float X0,Z0,W,D,H,Seed,Tier,Hue;public bool Bare;}
        private struct Rect2 {public float X0,X1,Z0,Z1;public Rect2(float x0,float x1,float z0,float z1){X0=x0;X1=x1;Z0=z0;Z1=z1;}}
        private static bool Overlaps(Lot l,Rect2 r)=>l.X0<r.X1&&l.X0+l.W>r.X0&&l.Z0-l.D<r.Z1&&l.Z0>r.Z0;

        // quality: 1 high, .6 reduced effects, .34 the low graphics tier (fewer motes and raindrops, no mist puffs).
        public static World Build(float quality=1)
        {
            var w=new World();bool low=quality<.5f;
            var custom=new List<Lot>();var keepOut=new List<Rect2>{new Rect2(-30,3,-4,20),new Rect2(4,48,-1300,80)};
            void Bld(float x0,float x1,float z0,float z1,float top,float hue,bool bare=true){custom.Add(new Lot{X0=x0,Z0=z1,W=x1-x0,D=z1-z0,H=top-Street,Seed=Math.Abs(x0*13+z0*7)%1000,Tier=.2f,Hue=hue,Bare=bare});keepOut.Add(new Rect2(x0-6,x1+6,z0-6,z1+6));}
            var bridges=new List<(float x0,float x1,float z0,float z1,float y)>();var huts=new List<(float x0,float x1,float z0,float z1,float y0,float y1)>();
            // Runner A: in from off-screen right over a roof, a 9 m leap down from its edge (x+18) onto a skybridge (x-1..x+10,
            // 3 m lower), into a hut at the bridge's left end; the leap lands mid-frame (NativeRooftopActors.Crowd: L1 16, Gap 9, L2 9.3).
            {var hut=At(.50f,.745f,54);float y=hut.Y,x=hut.X,z=hut.Z;w.Sites["runA"]=new RooftopSite{Id="runA",Position=new Vector3(x+34.5f,y+3,z),Yaw=-(float)Math.PI/2};
             Bld(x+18,x+90,z-9,z+9,y+3,.3f);bridges.Add((x-1,x+10,z-3,z+3,y));huts.Add((x-2,x+2.2f,z-3,z+3,y,y+3.2f));Bld(x-30,x-1,z-10,z+10,y+.01f,.8f);}
            // Runner B: further out and higher (clear of the duel roof), roof to roof with its leap in view, a hut at each end; its second
            // roof runs off the frame's right edge.
            {var st=At(.63f,.54f,96);float y=st.Y,x=st.X,z=st.Z;w.Sites["runB"]=new RooftopSite{Id="runB",Position=new Vector3(x,y,z),Yaw=(float)Math.PI/2};
             Bld(x-4,x+22,z-9,z+9,y,.3f);Bld(x+29,x+60,z-9,z+9,y,.3f);huts.Add((x-3.5f,x,z-2.5f,z+2.5f,y,y+3));huts.Add((x+53,x+57,z-2.5f,z+2.5f,y,y+3));}
            // The duel: blade and gunner 14 m apart on one roof; the watcher at its corner, watching.
            {var b=At(.76f,.665f,64);float y=b.Y;var g=new Vector3(b.X-9,y,b.Z-10.5f);
             w.Sites["blade"]=new RooftopSite{Id="blade",Position=b,Yaw=(float)Math.Atan2(g.X-b.X,g.Z-b.Z)};w.Sites["gunner"]=new RooftopSite{Id="gunner",Position=g,Yaw=(float)Math.Atan2(b.X-g.X,b.Z-g.Z)};
             Bld(Math.Min(b.X,g.X)-10,Math.Max(b.X,g.X)+10,Math.Min(b.Z,g.Z)-10,Math.Max(b.Z,g.Z)+10,y,.1f);
             var wp=new Vector3(b.X+7.5f,y,b.Z+5.5f);w.Sites["watch"]=new RooftopSite{Id="watch",Position=wp,Yaw=(float)Math.Atan2((b.X+g.X)/2-wp.X,(b.Z+g.Z)/2-wp.Z)};}
            // Landmark towers with their holo screens, turned to face the camera; nothing generated stands in front of them.
            Camera(0,out var eye0,out _);
            void Tower(string kind,float sx,float sy,float dist,float size,float aspect,float gain)
            {
                var c=At(sx,sy,dist);float half=size*.5f,ry=(float)Math.Atan2(eye0.X-c.X,eye0.Z-c.Z),back=half*Math.Abs((float)Math.Sin(ry))+2;
                keepOut.Add(new Rect2(c.X-size*.75f,c.X+size*.75f,c.Z-back-size*1.2f,c.Z+size*.6f));
                Bld(c.X-size*.6f,c.X+size*.6f,c.Z-size*1.1f-back,c.Z-back,c.Y+half*aspect+(kind=="chip"?2.5f:10+size*.25f),kind=="siren"?.6f:.2f,kind!="siren");
                w.Screens.Add(new RooftopScreen{Kind=kind,Center=c,Width=size,Height=size*aspect,Yaw=ry,Gain=gain});
            }
            Tower("siren",.82f,.21f,320,48,1,1.8f);Tower("arena",.57f,.13f,470,34,1,1.6f);Tower("chip",.665f,.43f,160,30,.75f,1.5f);Tower("banner",.955f,.33f,210,9,3,1.6f);
            var lots=Lots(custom,keepOut);w.LotCount=lots.Count;
            CityQuads(w,lots);Perch(w);
            foreach(var b in bridges){Box(w,b.x0,b.y-1.2f,b.z0,b.x1,b.y,b.z1,Lit,Albedo(.03f,.035f,.055f));Box(w,b.x0,b.y,b.z0,b.x1,b.y+1,b.z0+.15f,Lit,Albedo(.02f,.03f,.05f));Box(w,b.x0,b.y,b.z1-.15f,b.x1,b.y+1,b.z1,Lit,Albedo(.02f,.03f,.05f));
             foreach(float zz in new[]{b.z0,b.z1})NeonBar(w,new Vector3(b.x0,b.y+1,zz),new Vector3(b.x1,b.y+1,zz),.12f,new Vector3(.2f,2.2f,2.8f));}
            foreach(var h in huts){Box(w,h.x0,h.y0,h.z0,h.x1,h.y1,h.z1,Lit,Albedo(.035f,.04f,.06f));NeonBar(w,new Vector3(h.x0,h.y1-.12f,h.z1+.01f),new Vector3(h.x1,h.y1-.12f,h.z1+.01f),.12f,new Vector3(2.4f,.3f,2f));}
            // street glow strips along the avenue floor
            for(float z=60;z>-1500;z-=6){float k=.6f+.4f*Hash2((int)z,3);Quad(w,new Vector3(30,Street+.2f,z),new Vector3(62,Street+.2f,z),new Vector3(62,Street+.2f,z-5),new Vector3(30,Street+.2f,z-5),Vector3.UnitY,Vector4.Zero,Neon,new Vector4(.25f*k,.08f*k,.25f*k,1));}
            Traffic(w);Sprites(w,quality,low);
            var chip=w.Screens.Find(s=>s.Kind=="chip");w.SparkAt=new Vector3(chip.Center.X-chip.Width*.5f,chip.Center.Y+chip.Height*.5f,chip.Center.Z);
            return w;
        }
        private static Vector4 Albedo(float r,float g,float b)=>new Vector4(r,g,b,1);
        private static List<Lot> Lots(List<Lot> custom,List<Rect2> keepOut)
        {
            var lots=new List<Lot>();var r=new Random(77);const float B=34,S=16,step=B+S;
            for(int gz=-1;gz<34;gz++)for(int gx=-20;gx<24;gx++)
            {
                float x0=gx*step-8,z0=-gz*step-40;if(z0<-1700)continue;float wd=B-(r.Next()<.3f?8:0),dp=B-(r.Next()<.3f?8:0);
                float far=Clamp((-z0-200)/900,0,1);
                float h=Lerp(40,110,r.Next())+far*Lerp(0,190,(float)Math.Pow(r.Next(),1.6))+120*(float)Math.Pow(Fbm(gx*.35f,gz*.35f),3);
                if(r.Next()<.07f)h+=90+r.Next()*120;
                if(-z0<160&&Math.Abs(x0)<160)h=Math.Min(h,Lerp(45,112,r.Next()));
                var l=new Lot{X0=x0,Z0=z0,W=wd,D=dp,H=h,Seed=r.Next()*1000,Tier=r.Next(),Hue=r.Next()};
                bool skip=false;foreach(var k in keepOut)if(Overlaps(l,k)){skip=true;break;}if(!skip)lots.Add(l);
            }
            lots.AddRange(custom);return lots;
        }
        // ---- geometry helpers: quads in the source frame; the Unity boundary mirrors x and flips the winding ----
        private static void Quad(World w,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n,Vector4 uv,int mode,Vector4 attr)=>w.Quads.Add(new RooftopQuad{A=a,B=b,C=c,D=d,Normal=n,Uv=uv,Mode=mode,Attr=attr});
        // A box as five faces (no bottom). Facade boxes carry metric face coordinates (Uv) for the window grid.
        public static void Box(World w,float x0,float y0,float z0,float x1,float y1,float z1,int mode,Vector4 attr,Vector4? topAttr=null)
        {
            var top=topAttr??attr;Vector4 Roof(Vector4 a)=>mode==Facade?new Vector4(a.X,a.Y,a.Z,1):a;
            Quad(w,new Vector3(x0,y1,z1),new Vector3(x1,y1,z1),new Vector3(x1,y1,z0),new Vector3(x0,y1,z0),Vector3.UnitY,new Vector4(x0,z0,x1,z1),mode,Roof(top));
            Quad(w,new Vector3(x0,y0,z1),new Vector3(x1,y0,z1),new Vector3(x1,y1,z1),new Vector3(x0,y1,z1),Vector3.UnitZ,new Vector4(x0,y0,x1,y1),mode,attr);
            Quad(w,new Vector3(x1,y0,z0),new Vector3(x0,y0,z0),new Vector3(x0,y1,z0),new Vector3(x1,y1,z0),-Vector3.UnitZ,new Vector4(-x1,y0,-x0,y1),mode,attr);
            Quad(w,new Vector3(x1,y0,z1),new Vector3(x1,y0,z0),new Vector3(x1,y1,z0),new Vector3(x1,y1,z1),Vector3.UnitX,new Vector4(-z1,y0,-z0,y1),mode,attr);
            Quad(w,new Vector3(x0,y0,z0),new Vector3(x0,y0,z1),new Vector3(x0,y1,z1),new Vector3(x0,y1,z0),-Vector3.UnitX,new Vector4(z0,y0,z1,y1),mode,attr);
        }
        // An emissive bar between a and b at height `h` (below the given line), facing away from the box it trims.
        private static void NeonBar(World w,Vector3 a,Vector3 b,float h,Vector3 color)
        {
            var dir=b-a;var n=Vector3.Normalize(new Vector3(-dir.Z,0,dir.X));if(float.IsNaN(n.X))n=Vector3.UnitZ;var o=n*.05f;
            Quad(w,a+o-new Vector3(0,h,0),b+o-new Vector3(0,h,0),b+o,a+o,n,Vector4.Zero,Neon,new Vector4(color,1));
        }
        private static void CityQuads(World w,List<Lot> lots)
        {
            var trims=new List<(float x0,float x1,float z0,float z1,float y,float hue,float seed)>();
            foreach(var l in lots)
            {
                float top=Street+l.H,x0=l.X0,x1=l.X0+l.W,z0=l.Z0-l.D,z1=l.Z0,y=Street;int tiers=l.Tier>.55f?3:2;
                for(int k=0;k<tiers;k++)
                {
                    float ty=k==tiers-1?top:Lerp(Street,top,(k+1)/(tiers+.4f));Box(w,x0,y,z0,x1,ty,z1,Facade,new Vector4(l.Seed,k,l.Hue,0));
                    if(k==tiers-1||l.Tier>.8f)trims.Add((x0,x1,z0,z1,ty,l.Hue,l.Seed+k));y=ty;float ins=Math.Min(5,(x1-x0)*.12f);x0+=ins;x1-=ins;z0+=ins;z1-=ins;
                }
                var r=new Random((uint)Math.Floor(l.Seed*97));int n=l.Bare?0:1+(int)Math.Floor(r.Next()*3);
                for(int k=0;k<n;k++){float bw=2+r.Next()*5,bd=2+r.Next()*5,bh=1.5f+r.Next()*4,cx=Lerp(x0+bw,x1-bw,r.Next()),cz=Lerp(z0+bd,z1-bd,r.Next());Box(w,cx-bw/2,top,cz-bd/2,cx+bw/2,top+bh,cz+bd/2,Facade,new Vector4(l.Seed+50+k,9,l.Hue,0));}
            }
            var tr=new Random(5);
            foreach(var t in trims)
            {
                if(tr.Next()<.35f)continue;var col=t.hue<.55f?new Vector3(.2f,2.4f,3f):new Vector3(2.8f,.25f,2.2f);float k=.8f+tr.Next()*.8f;col*=k;
                NeonBar(w,new Vector3(t.x0,t.y,t.z1),new Vector3(t.x1,t.y,t.z1),.35f,col);NeonBar(w,new Vector3(t.x1,t.y,t.z1),new Vector3(t.x1,t.y,t.z0),.35f,col);
                NeonBar(w,new Vector3(t.x1,t.y,t.z0),new Vector3(t.x0,t.y,t.z0),.35f,col);NeonBar(w,new Vector3(t.x0,t.y,t.z0),new Vector3(t.x0,t.y,t.z1),.35f,col);
                if(Hash2((int)Math.Floor(t.seed),9)>=.65f)w.Glows.Add(new RooftopSprite{Position=new Vector3((t.x0+t.x1)/2,t.y,(t.z0+t.z1)/2),Color=t.hue<.55f?new Vector4(.15f,.7f,.9f,.18f):new Vector4(.8f,.1f,.6f,.18f),Size=Math.Max(t.x1-t.x0,8)*1.4f,Kind=Glow});
            }
        }
        // Our perch: 1 m voxel roof tiles, the facades with storey ledges, the lip (with a cyan neon strip), an AC unit with its
        // grille, a vent stack, the antenna mast, a stair hut, four puddles and the magenta ARENA blade sign on the corner.
        private static void Perch(World w)
        {
            for(float x=RoofX0;x<RoofX1;x+=1)for(float z=RoofZ0;z<RoofZ1;z+=1){float h=Hash2((int)Math.Floor(x*3),(int)Math.Floor(z*5));float y=h>.9f?.06f:0,k=.85f+.3f*h;Box(w,x,-.4f,z,Math.Min(x+1,RoofX1),y,Math.Min(z+1,RoofZ1),Lit,Albedo(.020f*k,.024f*k,.034f*k));}
            var wall=Albedo(.012f,.014f,.026f);Box(w,RoofX0,Street,RoofZ0-.01f,RoofX1,-.4f,RoofZ0,Lit,wall);Box(w,RoofX1,Street,RoofZ0,RoofX1+.01f,-.4f,RoofZ1,Lit,wall);
            for(float y=-4;y>-60;y-=3.6f){Box(w,RoofX0,y-.25f,RoofZ0-.35f,RoofX1+.35f,y,RoofZ0,Lit,Albedo(.02f,.022f,.035f));Box(w,RoofX1,y-.25f,RoofZ0-.35f,RoofX1+.35f,y,RoofZ1,Lit,Albedo(.02f,.022f,.035f));}
            Box(w,RoofX0,0,RoofZ0,RoofX1,LipTop,RoofZ0+LipDepth,Lit,Albedo(.018f,.02f,.032f),Albedo(.028f,.032f,.046f));Box(w,RoofX1-LipDepth,0,RoofZ0,RoofX1,LipTop,RoofZ1,Lit,Albedo(.018f,.02f,.032f),Albedo(.028f,.032f,.046f));
            NeonBar(w,new Vector3(RoofX1,LipTop,RoofZ0-.005f),new Vector3(RoofX0,LipTop,RoofZ0-.005f),.03f,new Vector3(.15f,1.6f,2f));
            Box(w,-3.9f,0,-1.25f,-2.1f,1.15f,-.05f,Lit,Albedo(.026f,.03f,.042f),Albedo(.036f,.04f,.054f));for(int k=0;k<4;k++)Box(w,-3.75f+k*.42f,1.15f,-1.1f,-3.5f+k*.42f,1.2f,-.2f,Lit,Albedo(.015f,.016f,.022f));
            Box(w,-5.2f,0,-.9f,-4.6f,1.6f,-.3f,Lit,Albedo(.045f,.05f,.065f));Box(w,-5.1f,1.6f,-.8f,-4.7f,1.9f,-.4f,Lit,Albedo(.03f,.034f,.045f));
            Box(w,-7.1f,0,-1.3f,-6.8f,9,-1,Lit,Albedo(.06f,.06f,.075f));Box(w,-7.5f,0,-1.7f,-6.4f,.25f,-.6f,Lit,Albedo(.05f,.05f,.065f));for(int k=1;k<5;k++)Box(w,-7.6f+k*.05f,k*1.8f,-1.25f,-6.3f-k*.05f,k*1.8f+.08f,-1.05f,Lit,Albedo(.05f,.05f,.06f));
            Box(w,-24,0,4,-15,4.2f,13,Lit,Albedo(.03f,.034f,.05f),Albedo(.045f,.05f,.068f));
            foreach(var (x,z,s) in new[]{(-1.2f,-.85f,1.3f),(.9f,-.6f,1f),(-4.4f,.6f,1.6f),(-1.6f,1.6f,2.2f)}){float hx=s*.8f,hz=s*.5f;Quad(w,new Vector3(x-hx,.012f,z+hz),new Vector3(x+hx,.012f,z+hz),new Vector3(x+hx,.012f,z-hz),new Vector3(x-hx,.012f,z-hz),Vector3.UnitY,new Vector4(0,0,1,1),Puddle,Vector4.Zero);}
            // the blade sign: a magenta box hanging off the corner below the lip, its lit letters facing out (+x)
            Box(w,RoofX1+.1f,-7.25f,-2.5f,RoofX1+.6f,-.75f,.1f,Neon,new Vector4(2.4f,.25f,1.9f,1));
            w.Screens.Add(new RooftopScreen{Kind="sign",Center=new Vector3(RoofX1+.62f,-4,-1.2f),Width=2.2f,Height=6,Yaw=(float)Math.PI/2,Gain=2.2f});
            w.SignLight=new Vector3(RoofX1+.4f,-1.2f,-1);w.CyanLight=new Vector3(-1.2f,.9f,-1.2f);w.Beacon=new Vector3(-6.95f,9.2f,-1.15f);
        }
        private static void Traffic(World w)
        {
            void Lane(Vector3[] pts,int n,float period,int dir,Vector3 col,bool near=false,float phase=0)=>w.Lanes.Add(new RooftopLane{Points=pts,Count=n,Period=period,Dir=dir,Color=col,Near=near,Phase=phase});
            Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
            Lane(new[]{V(18,-40,-30),V(18,-44,-200),V(16,-50,-500),V(12,-56,-900),V(8,-60,-1400)},16,48,1,V(.4f,1.6f,2.4f));
            Lane(new[]{V(34,-30,-30),V(34,-32,-200),V(36,-38,-500),V(40,-44,-900),V(44,-50,-1400)},14,48,-1,V(2.4f,.3f,1.6f));
            Lane(new[]{V(-600,-10,-36),V(-200,-9,-35),V(0,-8,-34),V(200,-9,-35),V(600,-12,-38)},2,24,1,V(.5f,2,2.6f),true,-.775f);
            Lane(new[]{V(700,10,-260),V(300,6,-240),V(0,4,-236),V(-300,8,-250),V(-800,14,-280)},10,48,-1,V(2.6f,.4f,1.8f));
            Lane(new[]{V(-900,32,-560),V(-300,28,-520),V(200,30,-540),V(900,38,-600)},12,48,1,V(.5f,1.9f,2.6f));
            Lane(new[]{V(26,-88,-30),V(26,-90,-400),V(26,-94,-1200)},12,48,1,V(1.6f,1.4f,1.2f));
        }
        // A point and tangent along a lane at u (0..1), centripetal Catmull-Rom through its points, arc-length parameterised by a
        // table built on first use.
        public static Vector3 LanePoint(RooftopLane lane,float u,out Vector3 tangent)
        {
            var table=Table(lane);float target=Clamp(u,0,1)*table[table.Length-1];int i=Array.BinarySearch(table,target);if(i<0)i=~i;i=Math.Max(1,Math.Min(table.Length-1,i));
            float t0=(i-1)/(float)(table.Length-1),t1=i/(float)(table.Length-1),f=(target-table[i-1])/Math.Max(1e-5f,table[i]-table[i-1]);float t=t0+(t1-t0)*f;
            var p=Spline(lane.Points,t);var q=Spline(lane.Points,Math.Min(1,t+.002f));var b=Spline(lane.Points,Math.Max(0,t-.002f));var d=q-b;tangent=d.LengthSquared()>0?Vector3.Normalize(d):Vector3.UnitZ;return p;
        }
        private static readonly Dictionary<RooftopLane,float[]> tables=new Dictionary<RooftopLane,float[]>();
        private static float[] Table(RooftopLane lane)
        {
            if(tables.TryGetValue(lane,out var t))return t;t=new float[201];var prev=Spline(lane.Points,0);for(int i=1;i<t.Length;i++){var p=Spline(lane.Points,i/200f);t[i]=t[i-1]+Vector3.Distance(prev,p);prev=p;}tables[lane]=t;return t;
        }
        public static float LaneLength(RooftopLane lane){var t=Table(lane);return t[t.Length-1];}
        private static Vector3 Spline(Vector3[] p,float t)
        {
            int n=p.Length-1;float x=Clamp(t,0,1)*n;int i=Math.Min(n-1,(int)Math.Floor(x));float u=x-i;var p0=p[Math.Max(0,i-1)];var p1=p[i];var p2=p[i+1];var p3=p[Math.Min(n,i+2)];
            float u2=u*u,u3=u2*u;return .5f*(2*p1+(-p0+p2)*u+(2*p0-5*p1+4*p2-p3)*u2+(-p0+3*p1-3*p2+p3)*u3);
        }
        // Where car k of a lane is at `time` (0..1 along it), its period dividing the loop (a car never pops at the loop seam).
        public static float CarU(RooftopLane lane,int k,float time){float u=k/(float)lane.Count+(time/lane.Period+lane.Phase)*lane.Dir;return u-(float)Math.Floor(u);}

        // ---- the sprite sprinkle (owner 2026-10-12: "add small particle effects sprinkled around"), thinned by quality ----
        private static void Sprites(World w,float quality,bool low)
        {
            var r=new Random(2026);
            foreach(var s in w.Screens){if(s.Kind=="sign")continue;Camera(0,out var eye,out _);var p=s.Center+Vector3.Normalize(eye-s.Center)*(Math.Max(s.Width,s.Height)*.9f);
                w.Glows.Add(new RooftopSprite{Position=p,Color=s.Kind=="siren"?new Vector4(.8f,.25f,1.2f,.35f):s.Kind=="chip"?new Vector4(.2f,.8f,1f,.35f):new Vector4(.2f,.9f,1.1f,.35f),Size=Math.Max(s.Width,s.Height)*2.4f,Kind=Glow});}
            w.Glows.Add(new RooftopSprite{Position=w.Beacon,Color=new Vector4(2.6f,.25f,1.4f,1),Size=.9f,Kind=Glow,Seed=.1f});
            w.Glows.Add(new RooftopSprite{Position=new Vector3(RoofX1+.7f,-3.5f,-1.3f),Color=new Vector4(2,.2f,1.6f,.5f),Size=9,Kind=Glow});
            w.Glows.Add(new RooftopSprite{Position=new Vector3(RoofX1+.7f,-4,-1.3f),Color=new Vector4(2.4f,.3f,2,.8f),Size=3,Kind=Glow});
            if(!low)for(int i=0;i<120;i++){float x=-500+r.Next()*1100,z=-60-r.Next()*1100,y=Street+10+r.Next()*90;bool m=r.Next()<.5f;w.Mist.Add(new RooftopSprite{Position=new Vector3(x,y,z),Color=m?new Vector4(.18f,.05f,.16f,.12f+r.Next()*.12f):new Vector4(.05f,.12f,.16f,.12f+r.Next()*.12f),Size=80+r.Next()*140,Seed=r.Next(),Kind=FlatMist});}
            // cyan specks near the pilot (firefly-like) and data motes / holo glints drifting between the towers
            int specks=(int)Math.Round(40*quality),motes=(int)Math.Round(110*quality);
            for(int i=0;i<specks;i++){var p=new Vector3(-4+r.Next()*8,-.5f+r.Next()*2.5f,-6+r.Next()*6);w.Motes.Add(new RooftopSprite{Position=p,Color=new Vector4(.3f,1.5f,1.9f,.6f+r.Next()*.4f),Size=.035f+r.Next()*.04f,Seed=r.Next(),Kind=Glow,Move=new Vector4((r.Next()-.5f)*.05f,.04f+r.Next()*.04f,(r.Next()-.5f)*.05f,Loop/2+r.Next()*0)});}
            for(int i=0;i<motes;i++){var p=new Vector3(-160+r.Next()*300,-60+r.Next()*80,-30-r.Next()*220);bool c=r.Next()<.6f;w.Motes.Add(new RooftopSprite{Position=p,Color=c?new Vector4(.3f,1.6f,2,.7f):new Vector4(1.8f,.3f,1.5f,.7f),Size=.25f+r.Next()*.5f,Seed=r.Next(),Kind=Mote,Move=new Vector4((r.Next()-.5f)*.2f,.15f+r.Next()*.2f,(r.Next()-.5f)*.2f,Loop/2)});}
            // steam from our two vents
            foreach(var v in new[]{new Vector3(-4.9f,1.9f,-.6f),new Vector3(-3f,1.25f,-.65f)})for(int i=0;i<(low?2:7);i++)w.Steam.Add(new RooftopSprite{Position=v,Color=new Vector4(.16f,.17f,.24f,.10f+r.Next()*.07f),Size=.9f+r.Next()*.8f,Seed=r.Next(),Kind=Puff,Move=new Vector4(.08f,.35f,-.02f,6)});
            // rain: a shell 5..23 m ahead of the lens, tinted magenta near the sign
            int rain=low?250:quality<.8f?500:900;Camera(0,out var e0,out _);
            for(int i=0;i<rain;i++){float a=(r.Next()-.5f)*1.5f-.72f,dd=5+r.Next()*18;var p=new Vector3(e0.X+(float)Math.Sin(a)*dd,-5+r.Next()*14,e0.Z-(float)Math.Cos(a)*dd);float near=(float)Math.Exp(-Math.Sqrt((p.X-RoofX1-.5f)*(p.X-RoofX1-.5f)+(p.Z+1.2f)*(p.Z+1.2f))/6);
                bool tint=near>.3f&&r.Next()<.5f;w.Rain.Add(new RooftopSprite{Position=p,Color=tint?new Vector4(1.4f,.35f,1.2f,.14f+near*.35f):new Vector4(.35f,.55f,.75f,.14f+near*.35f),Size=.45f+r.Next()*.4f,Seed=r.Next(),Kind=Rain,Move=new Vector4(.6f,-11,0,1.6f)});}
        }
    }
}
