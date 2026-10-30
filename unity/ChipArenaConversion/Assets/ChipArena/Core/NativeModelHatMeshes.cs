using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The hats made for the model (owner 2026-10-30: "Need to rework hats, they were all designed voxel style and a lot of them
    // have fitting issues (Player head shows outside of hat)"). The bank's hats are the voxel head's (about 36 cm across, flat
    // crowned), scaled onto the model's head (NativeModelHats.Fit), so a cap's or a beanie's walls stood inside the model's
    // hair and the scalp and hair showed through their sides and back. These are made for the model's own head: smooth shells
    // a centimetre or two round its skull (measured in Unity on the head bone, in its frame: x across, y up, z ahead; at the band,
    // BandY, 1.6 cm over the brows, the skull reaches 9.6 cm to each side, 10.9 cm ahead of and 11 cm behind BandZ, its top 21.2
    // cm over the bone). The hair is not laid flat (owner 2026-10-30: "Hats still cutting hair completely"; the pack's one
    // under-hat shape flattens the whole style, so a hat had to be sized round that and the scalp showed under an open one):
    // each pressing hat holds the hair as its own crown would (HairPress, Runtime/NativeVisualBodyPresentation.cs VisualHatHair),
    // the hair above its band tucked inside it, the hair under its band and brim kept as styled. Each hat:
    // - cap: a six-panel crown with stitched seams, a button and a curved bill;
    // - beanie: a ribbed knit dome, a folded cuff and a pom-pom;
    // - top hat: a tall slightly flared crown, a ribbon band, a curled brim with a rolled edge;
    // - wizard: a tall bent cone drooping back at its tip, a wide floppy brim, a band and gold stars;
    // - crown: a gold circlet with five points tipped by balls and jewels round it (open: the hair stands through it);
    // - cone: a party hat with a spiral stripe and a pom at its tip, sat on the crown;
    // - antenna: a headband with two coiled stalks ending in glowing balls;
    // - halo: a glowing ring floating over the hair;
    // - cowboy: a pinched crown with its top creased, a wide brim rolled up at the sides, a band with a buckle.
    // Each part is a parametric surface (a brim a slab with thickness), its vertex colours linear, its main material (the
    // colour a hat colour picks: NativeHatLooks) tinted in its twin (Build tinted: the main vertices carry their brightness
    // against the main colour and the body tag). Presentation only.
    public static class NativeModelHatMeshes
    {
        public static readonly string[] Hats={"cap","beanie","top","wizard","crown","cone","antenna","halo","cowboy"};
        public static bool Has(string hat)=>hat!=null&&Array.IndexOf(Hats,hat)>=0;
        /// <summary>The band round the head (head bone frame): its height, its centre ahead, its half width and half depth (the
        /// skull's reach there with a centimetre or two for the hair), and a pressing hat's crown's top.</summary>
        public const float BandY=.118f,BandZ=.025f,BandX=.121f,BandDepth=.127f,CrownTop=.215f;
        public const int Around=96;

        // A vertex's paint: its colour (sRGB), whether it is the hat's main material, and how much it glows; or the pilot's accent
        // glowing `Glow` strong, or its colour under the pilot's body colour (the robot suit's light strips and shell:
        // Core/NativeRobotSuit).
        internal struct Paint
        {
            public int Rgb;public float Shade;public bool Main;public float Glow;public bool Accent,Body;
            public Paint(int rgb,float shade=1,bool main=false,float glow=0){Rgb=rgb;Shade=shade;Main=main;Glow=glow;Accent=Body=false;}
            public static Paint Lit(float glow,float albedo=.35f)=>new Paint{Shade=albedo,Glow=glow,Accent=true};
        }
        // The surface kit (shared with the robot suit, whose parts ride other bones).
        internal sealed class Kit
        {
            public readonly List<SheldonSection> Parts=new List<SheldonSection>();public bool Tinted;public int MainRgb;
            public SheldonSection Part(string name,int bone=CapturedRig.Head){var s=new SheldonSection{Part=name,Bone=bone};Parts.Add(s);return s;}
            public int Vertex(SheldonSection s,Vector3 p,Vector3 n,Paint c)
            {
                if(c.Accent){var e=NativePortraitRules.ExportEmber;return s.Add(p,n,new Vector4(e*c.Shade,1),new Vector4(e*c.Glow,2));}
                var lin=NativePortraitRules.Linear(c.Rgb)*c.Shade;
                if(c.Body)return s.Add(p,n,new Vector4(lin,1),new Vector4(lin*c.Glow,1));
                if(Tinted&&c.Main)
                {
                    float level=NativeHatLooks.Luma(lin)/Math.Max(1e-5f,NativeHatLooks.Luma(NativePortraitRules.Linear(MainRgb)));
                    return s.Add(p,n,new Vector4(level,level,level,1),new Vector4(level*c.Glow,level*c.Glow,level*c.Glow,1));
                }
                return s.Add(p,n,new Vector4(lin,1),new Vector4(lin*c.Glow,0));
            }
            /// <summary>A surface over (u, v) in [0,1]^2, `nu` by `nv` cells (u wraps when `wrap`), facing along `outward`.</summary>
            public void Surface(SheldonSection s,int nu,int nv,bool wrap,Func<float,float,Vector3> P,Func<float,float,Vector3> outward,Func<float,float,Paint> paint)
            {
                int cols=wrap?nu:nu+1,first=s.Positions.Count;
                for(int j=0;j<=nv;j++)for(int i=0;i<cols;i++)
                {
                    float u=i/(float)nu,v=j/(float)nv;var p=P(u,v);var n=Normal(P,outward,u,v);Vertex(s,p,n,paint(u,v));
                }
                for(int j=0;j<nv;j++)for(int i=0;i<nu;i++)
                {
                    int i1=wrap?(i+1)%nu:i+1;int a=first+j*cols+i,b=first+j*cols+i1,c=first+(j+1)*cols+i1,d=first+(j+1)*cols+i;Quad(s,a,b,c,d);
                }
            }
            /// <summary>A slab: the mid surface P thickened by `thick` along its normal, its two faces and its four edges.</summary>
            public void Slab(SheldonSection s,int nu,int nv,bool wrap,float thick,Func<float,float,Vector3> P,Func<float,float,Vector3> up,Func<float,float,Paint> top,Func<float,float,Paint> under)
            {
                Vector3 N(float u,float v)=>Normal(P,up,u,v);
                Surface(s,nu,nv,wrap,(u,v)=>P(u,v)+N(u,v)*(thick*.5f),up,top);
                Surface(s,nu,nv,wrap,(u,v)=>P(u,v)-N(u,v)*(thick*.5f),(u,v)=>-up(u,v),under);
                // The edges: v=1 and v=0 (and u=0, u=1 when open), each a strip from the top face down to the under face.
                Rim(s,nu,wrap,1,thick,P,up,top);Rim(s,nu,wrap,0,thick,P,up,top);
                if(!wrap){Side(s,nv,0,thick,P,up,top);Side(s,nv,1,thick,P,up,top);}
            }
            // A slab's edge along u at v=`v`: from the top face down to the under face, facing out of the slab.
            private void Rim(SheldonSection s,int nu,bool wrap,float v,float thick,Func<float,float,Vector3> P,Func<float,float,Vector3> up,Func<float,float,Paint> paint)
            {
                int cols=wrap?nu:nu+1,f=s.Positions.Count;
                for(int i=0;i<cols;i++)
                {
                    float u=i/(float)nu;var n=Normal(P,up,u,v);var p=P(u,v);
                    var outward=v>.5f?P(u,v)-P(u,v-.02f):P(u,v)-P(u,v+.02f);outward-=n*Vector3.Dot(outward,n);outward=outward.LengthSquared()>1e-12f?Vector3.Normalize(outward):n;
                    Vertex(s,p+n*(thick*.5f),outward,paint(u,v));Vertex(s,p-n*(thick*.5f),outward,paint(u,v));
                }
                for(int i=0;i<nu;i++){int i1=wrap?(i+1)%nu:i+1;Quad(s,f+2*i,f+2*i1,f+2*i1+1,f+2*i+1);}
            }
            private void Side(SheldonSection s,int nv,float u,float thick,Func<float,float,Vector3> P,Func<float,float,Vector3> up,Func<float,float,Paint> paint)
            {
                int f=s.Positions.Count;
                for(int j=0;j<=nv;j++)
                {
                    float v=j/(float)nv;var n=Normal(P,up,u,v);var p=P(u,v);var outward=u>.5f?P(u,v)-P(u-.02f,v):P(u,v)-P(u+.02f,v);outward-=n*Vector3.Dot(outward,n);outward=outward.LengthSquared()>1e-12f?Vector3.Normalize(outward):n;
                    Vertex(s,p+n*(thick*.5f),outward,paint(u,v));Vertex(s,p-n*(thick*.5f),outward,paint(u,v));
                }
                for(int j=0;j<nv;j++)Quad(s,f+2*j,f+2*j+2,f+2*j+3,f+2*j+1);
            }
            /// <summary>A tube along a path (t in [0,1]) of radius `r(t)`, `sides` round, its ends capped.</summary>
            public void Tube(SheldonSection s,int n,int sides,Func<float,Vector3> path,Func<float,float> r,Func<float,float,Paint> paint,bool closed=false)
            {
                Vector3 T(float t){var a=path(Math.Max(0,t-.002f));var b=path(Math.Min(1,t+.002f));if(closed){a=path((t-.002f+1)%1);b=path((t+.002f)%1);}var d=b-a;return d.LengthSquared()>1e-14f?Vector3.Normalize(d):Vector3.UnitY;}
                Surface(s,sides,n,true,(u,t)=>{var tt=T(t);var a=Vector3.Normalize(Vector3.Cross(tt,Math.Abs(tt.Y)<.95f?Vector3.UnitY:Vector3.UnitX));var b=Vector3.Cross(tt,a);float th=u*MathF.PI*2;return path(t)+(a*MathF.Cos(th)+b*MathF.Sin(th))*r(t);},
                    (u,t)=>{var tt=T(t);var a=Vector3.Normalize(Vector3.Cross(tt,Math.Abs(tt.Y)<.95f?Vector3.UnitY:Vector3.UnitX));var b=Vector3.Cross(tt,a);float th=u*MathF.PI*2;return a*MathF.Cos(th)+b*MathF.Sin(th);},paint);
                if(!closed){Cap(s,path(0),-T(0),r(0),sides,paint(0,0));Cap(s,path(1),T(1),r(1),sides,paint(0,1));}
            }
            public void Cap(SheldonSection s,Vector3 at,Vector3 n,float r,int sides,Paint c)
            {
                if(r<1e-5f)return;var a=Vector3.Normalize(Vector3.Cross(n,Math.Abs(n.Y)<.95f?Vector3.UnitY:Vector3.UnitX));var b=Vector3.Cross(n,a);int centre=Vertex(s,at,n,c),f=s.Positions.Count;
                for(int i=0;i<sides;i++){float th=i*MathF.PI*2/sides;Vertex(s,at+(a*MathF.Cos(th)+b*MathF.Sin(th))*r,n,c);}
                for(int i=0;i<sides;i++)Tri(s,centre,f+i,f+(i+1)%sides);
            }
            /// <summary>A lumpy ball (a pom, a jewel, a tip): `lump` of its radius in broad bumps.</summary>
            public void Ball(SheldonSection s,Vector3 at,float r,float lump,int seed,Func<Vector3,Paint> paint,int rings=10,int sides=16)
            {
                Vector3 D(float u,float v){float th=u*MathF.PI*2,ph=v*MathF.PI;return new Vector3(MathF.Sin(ph)*MathF.Sin(th),MathF.Cos(ph),MathF.Sin(ph)*MathF.Cos(th));}
                float R(Vector3 d)=>r*(1+lump*(MathF.Sin(d.X*7+seed)*MathF.Sin(d.Y*6+seed*1.7f)*MathF.Sin(d.Z*8+seed*2.3f)));
                Surface(s,sides,rings,true,(u,v)=>at+D(u,v)*R(D(u,v)),(u,v)=>D(u,v),(u,v)=>paint(D(u,v)));
            }
            private static Vector3 Normal(Func<float,float,Vector3> P,Func<float,float,Vector3> outward,float u,float v)
            {
                const float e=.002f;var du=P(u+e,v)-P(u-e,v);var dv=P(u,Math.Min(1,v+e))-P(u,Math.Max(0,v-e));var n=Vector3.Cross(du,dv);var o=outward(u,v);
                if(n.LengthSquared()<1e-16f)return o.LengthSquared()>1e-12f?Vector3.Normalize(o):Vector3.UnitY;n=Vector3.Normalize(n);return Vector3.Dot(n,o)<0?-n:n;
            }
            public static void Tri(SheldonSection s,int a,int b,int c)
            {
                var pa=s.Positions[a];var f=Vector3.Cross(s.Positions[b]-pa,s.Positions[c]-pa);if(f.LengthSquared()<1e-18f)return;
                if(Vector3.Dot(f,s.Normals[a]+s.Normals[b]+s.Normals[c])>=0)s.Tri(a,b,c);else s.Tri(a,c,b);
            }
            public static void Quad(SheldonSection s,int a,int b,int c,int d){Tri(s,a,b,c);Tri(s,a,c,d);}
        }

        // The band ellipse's point at angle `th` (0 ahead, toward +x positive), `scale` times its size, at height y.
        private static Vector3 Band(float th,float scale,float y)=>new Vector3(BandX*scale*MathF.Sin(th),y-Back(th),BandZ+BandDepth*scale*MathF.Cos(th));
        // A pressing hat's band dips down the back of the head (BackDrop at the middle of the back), as a hat sits on the occiput.
        // The open hats and the cone (level) keep it level.
        private static bool level;
        private static float Back(float th){if(level)return 0;float b=Math.Max(0,-MathF.Cos(th));return BackDrop*b*MathF.Sqrt(b);}
        public const float BackDrop=.042f;
        private static Vector3 Out(float th)=>Vector3.Normalize(new Vector3(MathF.Sin(th)/BandX,0,MathF.Cos(th)/BandDepth));
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static float Wrap(float a){a%=MathF.PI*2;if(a<0)a+=MathF.PI*2;return a;}
        private static float Near(float th,float at){float d=Math.Abs(Wrap(th-at));return Math.Min(d,MathF.PI*2-d);}

        /// <summary>A hat's sections for the model's head (null for a hat not made for it), tinted: its main vertices carry the
        /// body tag and their brightness against its main colour (NativeHatLooks).</summary>
        public static SheldonSection[] Build(string hat,bool tinted)
        {
            if(!Has(hat))return null;var mains=NativeHatLooks.Mains(hat);var k=new Kit{Tinted=tinted,MainRgb=mains.Length>0?mains[0]:0xffffff};
            lock(Hats){level=!NativeModelHats.Presses(hat)||hat=="cone";
            switch(hat)
            {
                case "cap":Cap(k);break;case "beanie":Beanie(k);break;case "top":TopHat(k);break;case "wizard":Wizard(k);break;case "crown":Crown(k);break;
                case "cone":Cone(k);break;case "antenna":Antenna(k);break;case "halo":Halo(k);break;case "cowboy":Cowboy(k);break;
            }
            level=false;}
            return k.Parts.ToArray();
        }

        /// <summary>The hats that hold the hair under them (HairPress): the crowned ones and the cone. The crown, the antenna and the
        /// halo stand in the hair.</summary>
        public static bool PressesHair(string hat)=>hat=="cap"||hat=="beanie"||hat=="top"||hat=="wizard"||hat=="cowboy"||hat=="cone";
        // Each pressing hat's crown as its builder makes it, the surface HairPress holds the hair inside: u round from the front,
        // v from its band up to its top.
        private static Func<float,float,Vector3> Shell(string hat)
        {
            switch(hat)
            {
                case "cap":return CapCrown;case "beanie":return BeanieCrown;case "top":return TopCrown;case "wizard":return WizardCone;
                case "cowboy":return CowboyCrown;case "cone":return ConeShell;default:return null;
            }
        }

        /// <summary>
        /// The hair under a pressing hat (PressesHair), in the head bone's frame. Press moves a point the hat's crown would not hold
        /// (outside its inner surface, Margin in from the shell, over its band) in toward Centre onto that surface, and a point just
        /// under the band (within Flare) in at its own height onto a rim widening without bound away from the band (no step where
        /// the hair goes free), so the hair under a band or a brim keeps its
        /// style and stands out from under it; lower down the hair is untouched. The cone holds only the hair in its own
        /// footprint: the hair round it stands. Each hat's crown is profiled once, Columns times round the head.
        /// </summary>
        public sealed class HairPress
        {
            public const float Margin=.004f,Flare=.03f;public const int Columns=128,Steps=96;
            public static readonly Vector3 Centre=new Vector3(0,BandY-.015f,BandZ);
            private static readonly Dictionary<string,HairPress> made=new Dictionary<string,HairPress>();
            // Per column, the crown's profile up from its band: each point's height and reach (band units, Margin in); the crown's
            // axis's offset ahead (a wizard's cone bends back) by height.
            private readonly float[] ys=new float[Columns*(Steps+1)],rs=new float[Columns*(Steps+1)],dz=new float[Steps+1],dzY=new float[Steps+1];
            private readonly bool footprint,bent;
            /// <summary>The press for `hat`, made once (null for a hat that does not press the hair).</summary>
            public static HairPress Of(string hat)
            {
                if(!PressesHair(hat))return null;
                lock(Hats)
                {
                    if(made.TryGetValue(hat,out var made1))return made1;level=hat=="cone";HairPress p;try{p=new HairPress(hat);}finally{level=false;}
                    made[hat]=p;return p;
                }
            }
            private HairPress(string hat)
            {
                var shell=Shell(hat);footprint=hat=="cone";
                for(int j=0;j<=Steps;j++){float v=j/(float)Steps;var f=shell(0,v);var b=shell(.5f,v);dz[j]=(f.Z+b.Z)*.5f-BandZ;dzY[j]=(f.Y+b.Y)*.5f;bent|=Math.Abs(dz[j])>1e-5f;}
                var q=new Vector2[Steps+1];
                for(int i=0;i<Columns;i++)
                {
                    float u=i/(float)Columns,th=u*MathF.PI*2,metres=MathF.Sqrt(MathF.Pow(BandX*MathF.Sin(th),2)+MathF.Pow(BandDepth*MathF.Cos(th),2));
                    for(int j=0;j<=Steps;j++){var p=shell(u,j/(float)Steps);float x=p.X/BandX,z=(p.Z-BandZ-dz[j])/BandDepth;q[j]=new Vector2(MathF.Sqrt(x*x+z*z)*metres,p.Y);}
                    // Margin in from the shell, square to the profile (up the wall it moves in, over the top down).
                    for(int j=0;j<=Steps;j++)
                    {
                        var d=q[Math.Min(Steps,j+1)]-q[Math.Max(0,j-1)];var n=d.LengthSquared()>1e-14f?Vector2.Normalize(new Vector2(-d.Y,d.X)):new Vector2(-1,0);var m=q[j]+n*Margin;
                        ys[i*(Steps+1)+j]=m.Y;rs[i*(Steps+1)+j]=Math.Max(0,m.X)/metres;
                    }
                }
            }
            private float Dz(float y)
            {
                if(!bent||y<=dzY[0])return bent?dz[0]:0;
                for(int j=0;j<Steps;j++)if(y<=dzY[j+1]){float t=(y-dzY[j])/Math.Max(1e-6f,dzY[j+1]-dzY[j]);return dz[j]+(dz[j+1]-dz[j])*t;}
                return dz[Steps];
            }
            /// <summary>Whether `p` lies over the hat's band (by `above` at least), where the hat covers it.</summary>
            public bool Over(Vector3 p,float above=0)
            {
                float x=p.X/BandX,z=(p.Z-BandZ-Dz(p.Y))/BandDepth;return Rise(p)>=above&&(!footprint||MathF.Sqrt(x*x+z*z)<=rs[Column(x,z)]);
            }
            /// <summary>How high `p` stands over the hat's band where it is (under it: below zero).</summary>
            public float Rise(Vector3 p){float x=p.X/BandX,z=(p.Z-BandZ-Dz(p.Y))/BandDepth;return p.Y-ys[Column(x,z)];}
            /// <summary>How fully the hat holds hair it pressed from `from` to `to`: all of it moved HoldMove or more (it moves with the
            /// head then, its cloth's swing would carry it out through the hat, or far off along a lock it was taken from), less as
            /// it was moved less (the hair the hat barely touches swings on with its cloth).</summary>
            public static float Hold(Vector3 from,Vector3 to)=>Smooth(Vector3.Distance(from,to)/HoldMove);
            public const float HoldMove=.02f;
            private static int Column(float x,float z)=>(int)MathF.Round(Wrap(MathF.Atan2(x,z))/(MathF.PI*2)*Columns)%Columns*(Steps+1);
            /// <summary>Whether the hat leaves `p` where it is: inside its crown, or free of it.</summary>
            public bool Holds(Vector3 p)
            {
                float x=p.X/BandX,z=(p.Z-BandZ-Dz(p.Y))/BandDepth,rho=MathF.Sqrt(x*x+z*z);int o=Column(x,z);float y0=ys[o];
                if(p.Y<y0)
                {
                    // (The rim widens without bound toward Flare under the band: no step where the hair goes free.)
                    if(footprint||p.Y<=y0-Flare)return true;float k=(y0-p.Y)/Flare;return rho<=rs[o]*(1+3*k*k/(1-k));
                }
                if(footprint&&rho>rs[o]+.1f)return true;
                // Inside the profile: an odd count of its crossings at this height lies farther out.
                int count=0;
                for(int j=0;j<Steps;j++)
                {
                    float a=ys[o+j],b=ys[o+j+1];if(a>p.Y==b>p.Y)continue;float t=(p.Y-a)/(b-a);if(rs[o+j]+(rs[o+j+1]-rs[o+j])*t>rho)count++;
                }
                return (count&1)==1;
            }
            /// <summary>
            /// A hair piece under the hat: its vertices at rest on the head (`rest`, head bone frame) and its triangles; into
            /// `pressed`, where the hat puts each, and `hold`, how fully the hat holds each (Hold). A lock (the piece's vertices
            /// joined by its triangles, welded where they meet) the press would tear, a triangle's edge stretched over Tear (a
            /// tail streaming out from under the crown, its root tucked in, the rest left out), goes under the hat whole: drawn
            /// nowhere (every vertex at Centre, in the head), as a beanie takes a ponytail in. False when the hat moves nothing.
            /// </summary>
            public bool PressPiece(Vector3[] rest,int[] triangles,Vector3[] pressed,float[] hold)
            {
                bool moved=false;
                for(int i=0;i<rest.Length;i++){pressed[i]=Press(rest[i]);hold[i]=Hold(rest[i],pressed[i]);moved|=pressed[i]!=rest[i];}
                if(!moved)return false;
                var lockOf=new int[rest.Length];for(int i=0;i<lockOf.Length;i++)lockOf[i]=i;
                int Find(int x){while(lockOf[x]!=x)x=lockOf[x]=lockOf[lockOf[x]];return x;}
                var at=new Dictionary<(int,int,int),int>();
                for(int i=0;i<rest.Length;i++){var key=((int)MathF.Round(rest[i].X*2e4f),(int)MathF.Round(rest[i].Y*2e4f),(int)MathF.Round(rest[i].Z*2e4f));if(at.TryGetValue(key,out int j))lockOf[Find(i)]=Find(j);else at[key]=i;}
                for(int i=0;i+2<triangles.Length;i+=3){lockOf[Find(triangles[i])]=Find(triangles[i+1]);lockOf[Find(triangles[i+1])]=Find(triangles[i+2]);}
                var torn=new HashSet<int>();
                for(int i=0;i+2<triangles.Length;i+=3)for(int e=0;e<3;e++)
                {
                    int a=triangles[i+e],b=triangles[i+(e+1)%3];
                    if(Vector3.Distance(pressed[a],pressed[b])-Vector3.Distance(rest[a],rest[b])>Tear){torn.Add(Find(a));break;}
                }
                if(torn.Count>0)for(int i=0;i<rest.Length;i++)if(torn.Contains(Find(i))){pressed[i]=Centre;hold[i]=1;}
                return true;
            }
            public const float Tear=.04f;
            /// <summary>Where the hat puts the hair at `p`.</summary>
            public Vector3 Press(Vector3 p)
            {
                if(Holds(p))return p;
                float dz=Dz(p.Y),x=p.X/BandX,z=(p.Z-BandZ-dz)/BandDepth;int o=Column(x,z);
                if(p.Y<ys[o]&&!footprint)
                {
                    // Under the band: in toward the crown's axis at its own height, onto the widening rim.
                    float k=(ys[o]-p.Y)/Flare,s=rs[o]*(1+3*k*k/(1-k))/MathF.Sqrt(x*x+z*z)*.9999f;
                    return new Vector3(p.X*s,p.Y,BandZ+dz+(p.Z-BandZ-dz)*s);
                }
                var d=p-Centre;float lo=0,hi=1;const int March=32;
                for(int s=1;s<=March;s++){float t=s/(float)March;if(!Holds(Centre+d*t)){hi=t;lo=(s-1)/(float)March;break;}}
                for(int s=0;s<10;s++){float t=(lo+hi)*.5f;if(Holds(Centre+d*t))lo=t;else hi=t;}
                return Centre+d*lo;
            }
        }

        // A pressing hat's crown: a dome from the band (y0) to `top`, its sides near upright before they round over; `lift` raises
        // its front (none at the back, nor at the peak, where the crown meets itself); `bulge` its widest share over the band.
        private static Vector3 Dome(float u,float v,float y0,float top,float bulge,float lift,float power=.72f)
        {
            float th=u*MathF.PI*2,ph=v*MathF.PI/2,r=MathF.Pow(Math.Max(0,MathF.Cos(ph)),power)*(1+bulge*MathF.Sin(ph*2)),y=y0+(top-y0)*MathF.Sin(ph)+lift*(1+MathF.Cos(th))*.5f*MathF.Sin(ph)*Smooth(r/.4f);
            // (The band's dip down the back fades up the crown: its top stays level.)
            var p=Band(th,r,y);p.Y+=Back(th)*MathF.Sin(ph);return p;
        }
        private static Vector3 DomeOut(float u,float v,float y0){float th=u*MathF.PI*2;return Out(th)*MathF.Cos(v*MathF.PI/2)+Vector3.UnitY*MathF.Sin(v*MathF.PI/2)*1.2f;}

        // ---- cap: six panels with stitched seams, a button, a curved bill ----
        private static Vector3 CapCrown(float u,float v)=>Dome(u,v,BandY-.006f,CrownTop+.012f,.05f,.012f,.55f);
        private static void Cap(Kit k)
        {
            int main=0xff6b35;var s=k.Part("cap");
            float[] seams={MathF.PI/6,MathF.PI/2,5*MathF.PI/6,7*MathF.PI/6,3*MathF.PI/2,11*MathF.PI/6};
            Paint Crown(float u,float v){float th=u*MathF.PI*2,d=9;foreach(var a in seams)d=Math.Min(d,Near(th,a));float seam=d<.035f?.62f:d<.07f?.86f:1;return new Paint(main,seam*(.95f+.05f*v),true);}
            k.Surface(s,Around,18,true,CapCrown,(u,v)=>DomeOut(u,v,BandY),Crown);
            // The inside band under the crown's edge (seen under the bill).
            k.Surface(s,Around,1,true,(u,v)=>Band(u*MathF.PI*2,.985f,BandY-.006f+v*.018f),(u,v)=>-Out(u*MathF.PI*2),(u,v)=>new Paint(main,.45f,true));
            // The button and the eyelets.
            k.Ball(s,new Vector3(0,CrownTop+.017f,BandZ+.004f),.0105f,.05f,3,d=>new Paint(main,.82f,true),6,12);
            foreach(var a in new[]{MathF.PI/3,2*MathF.PI/3,4*MathF.PI/3,5*MathF.PI/3})
            {var at=CapCrown(a/(MathF.PI*2),.62f);k.Ball(s,at,.0045f,0,1,d=>new Paint(0x2a2a30),4,8);}
            // The bill: the band's front 160 degrees out 8.6 cm, curved down at its sides and its tip.
            k.Slab(s,40,8,false,.007f,(u,v)=>
            {
                float th=(u-.5f)*MathF.PI*.92f,w=(u-.5f)*2,reach=.086f*(1-.45f*w*w);var at=Band(th,1.005f,BandY-.003f);var o=Out(th);
                var p=at+o*(reach*v);p.Y-=.012f*v*v+.022f*w*w*v+.006f*v;return p;
            },(u,v)=>Vector3.UnitY,(u,v)=>new Paint(main,.92f,true),(u,v)=>new Paint(0x3b3e46,1));
        }

        // ---- beanie: a ribbed knit dome, a folded cuff, a pom-pom ----
        // (What holds the hair: the dome as from the cuff's foot.)
        private static Vector3 BeanieCrown(float u,float v)=>Dome(u,v,BandY-.026f,CrownTop+.016f,.05f,0,.6f);
        private static void Beanie(Kit k)
        {
            int main=0xd277ff,cuff=0xb760e8;var s=k.Part("beanie");
            k.Surface(s,Around,20,true,(u,v)=>
            {
                var p=Dome(u,v,BandY-.012f,CrownTop+.016f,.05f,0,.6f);float th=u*MathF.PI*2,rib=.0028f*MathF.Cos(th*40)*MathF.Cos(v*MathF.PI/2);p+=Out(th)*rib;return p;
            },(u,v)=>DomeOut(u,v,BandY),(u,v)=>{float th=u*MathF.PI*2;return new Paint(main,.9f+.1f*MathF.Cos(th*40),true);});
            // The cuff: a thick ribbed fold round the band.
            k.Surface(s,Around*2,6,true,(u,v)=>
            {
                float th=u*MathF.PI*2,bulge=MathF.Sin(v*MathF.PI),rib=.0034f*MathF.Cos(th*56);return Band(th,1.035f+.03f*bulge,BandY-.026f+v*.05f)+Out(th)*rib;
            },(u,v)=>Out(u*MathF.PI*2)+Vector3.UnitY*(v-.5f),(u,v)=>{float th=u*MathF.PI*2;return new Paint(cuff,.86f+.14f*MathF.Cos(th*56),true);});
            k.Surface(s,Around,1,true,(u,v)=>Band(u*MathF.PI*2,1.0f,BandY-.026f+v*.002f),(u,v)=>-Vector3.UnitY,(u,v)=>new Paint(cuff,.6f,true));
            // The pom-pom: a lumpy fluffy ball on top.
            k.Ball(s,new Vector3(0,CrownTop+.045f,BandZ+.006f),.034f,.14f,7,d=>new Paint(main,1.08f+.12f*d.Y,true),12,20);
        }

        // ---- top hat: a tall flared crown, a ribbon band, a curled brim with a rolled edge ----
        private const float TopY0=BandY+.008f,TopTop=BandY+.215f;
        // (The band's dip down the back fades up the wall: the top is level.)
        private static Vector3 TopWall(float u,float v){float th=u*MathF.PI*2;var p=Band(th,.96f+.07f*v*v,TopY0+(TopTop-TopY0)*v);p.Y+=Back(th)*v;return p;}
        // (What holds the hair: the wall, then the flat top in to the middle.)
        private static Vector3 TopCrown(float u,float v)=>v<=.85f?TopWall(u,v/.85f):Level(Band(u*MathF.PI*2,1.03f*(1-(v-.85f)/.15f),TopTop),u);
        private static Vector3 Level(Vector3 p,float u){p.Y+=Back(u*MathF.PI*2);return p;}
        private static void TopHat(Kit k)
        {
            int main=0x191a20,band=0x7a2230;var s=k.Part("top");const float y0=TopY0,top=TopTop;
            k.Surface(s,Around,10,true,TopWall,(u,v)=>Out(u*MathF.PI*2),(u,v)=>new Paint(main,1,true));
            // The flat top, its edge rounded.
            k.Surface(s,Around,6,true,(u,v)=>{float th=u*MathF.PI*2,r=(1-v*v)*1.03f;return Level(Band(th,r,top+.006f*MathF.Sin(MathF.Min(1,(1-v)*3)*MathF.PI/2)*(v>.0f?1:0)),u);},(u,v)=>Vector3.UnitY,(u,v)=>new Paint(main,1.06f,true));
            // The ribbon band, a little proud of the crown, and its bow on the left side.
            k.Surface(s,Around,3,true,(u,v)=>{float th=u*MathF.PI*2,w=(.006f+v*.038f)/(top-y0);var p=Band(th,.975f+.004f+.07f*w*w,y0+.006f+v*.038f);p.Y+=Back(th)*w;return p;},(u,v)=>Out(u*MathF.PI*2),(u,v)=>new Paint(band,.9f+.1f*v));
            var bow=Band(-MathF.PI/2,.99f,y0+.026f);k.Ball(s,bow,.012f,.25f,5,d=>new Paint(band,.85f),6,10);
            // The brim: out 4.5 cm, curled up at the sides, a rolled edge.
            Func<float,float,Vector3> brim=(u,v)=>{float th=u*MathF.PI*2,side=MathF.Pow(Math.Abs(MathF.Sin(th)),2);var p=Band(th,.96f+.4f*v,y0+.002f);p.Y+=.028f*side*v*v-.004f*v;return p;};
            k.Slab(s,Around,6,true,.006f,brim,(u,v)=>Vector3.UnitY,(u,v)=>new Paint(main,.92f,true),(u,v)=>new Paint(main,.7f,true));
            k.Tube(s,Around,8,t=>brim(t,1),t=>.006f,(u,t)=>new Paint(main,.85f,true),true);
        }

        // ---- wizard: a bent cone drooping back, a wide floppy brim, a band, gold stars ----
        private static Vector3 WizardCone(float u,float t)
        {
            // (Its foot follows the band down the back, onto the brim.)
            float th=u*MathF.PI*2,r=MathF.Pow(1-t,.85f+.4f*t);var c=new Vector3(0,BandY+.36f*t-.05f*t*t*t,BandZ-.16f*t*t*t);var b=Band(th,.99f*r,0);return new Vector3(b.X,c.Y+b.Y*(1-t)*(1-t),c.Z+(b.Z-BandZ));
        }
        private static void Wizard(Kit k)
        {
            int main=0x904bea,dark=0x6334a6,gold=0xffd269;var s=k.Part("wizard");float y0=BandY;
            k.Surface(s,Around,24,true,WizardCone,(u,t)=>Out(u*MathF.PI*2)+Vector3.UnitY*.3f,(u,t)=>new Paint(main,.92f+.08f*MathF.Cos(u*MathF.PI*2*3),true));
            // A floppy brim, waved and drooping.
            k.Slab(s,Around,8,true,.006f,(u,v)=>{float th=u*MathF.PI*2;var p=Band(th,.98f+.9f*v,y0+.002f);p.Y-=.03f*v*v*(1+.6f*MathF.Sin(th*3+.7f));return p;},(u,v)=>Vector3.UnitY,(u,v)=>new Paint(main,.85f,true),(u,v)=>new Paint(dark,1,true));
            // The band and its buckle, and gold stars over the cone.
            k.Surface(s,Around,2,true,(u,v)=>Band(u*MathF.PI*2,1.005f,y0+.004f+v*.026f),(u,v)=>Out(u*MathF.PI*2),(u,v)=>new Paint(gold,.85f));
            var rng=new Random(17);
            for(int i=0;i<7;i++)
            {
                float t=.15f+.6f*i/6f,th=(float)(rng.NextDouble()*Math.PI*2);var at=WizardCone(th/(MathF.PI*2),t);var n=Out(th);
                Star(k,s,at+n*.002f,n,.012f*(1-.4f*t),gold);
            }
        }
        // A five-pointed star lying on a surface at `at` facing `n`.
        private static void Star(Kit k,SheldonSection s,Vector3 at,Vector3 n,float r,int rgb)
        {
            var a=Vector3.Normalize(Vector3.Cross(n,Math.Abs(n.Y)<.95f?Vector3.UnitY:Vector3.UnitX));var b=Vector3.Cross(n,a);var c=new Paint(rgb,1,false,.35f);
            int centre=k.Vertex(s,at+n*.002f,n,c),f=s.Positions.Count;
            for(int i=0;i<10;i++){float th=i*MathF.PI/5,rr=i%2==0?r:r*.42f;k.Vertex(s,at+(a*MathF.Sin(th)+b*MathF.Cos(th))*rr,n,c);}
            for(int i=0;i<10;i++)Kit.Tri(s,centre,f+i,f+(i+1)%10);
        }

        // ---- crown: a gold circlet, five points tipped by balls, jewels (open: the hair stands through) ----
        private static void Crown(Kit k)
        {
            int gold=0xffd269,deep=0xffbf3e;var s=k.Part("crown");float y0=BandY+.072f,scale=.92f;
            k.Surface(s,Around,4,true,(u,v)=>{float th=u*MathF.PI*2;return Band(th,scale+.02f*MathF.Sin(v*MathF.PI),y0+v*.034f);},(u,v)=>Out(u*MathF.PI*2),(u,v)=>new Paint(gold,.9f+.12f*MathF.Sin(v*MathF.PI),true));
            k.Surface(s,Around,1,true,(u,v)=>Band(u*MathF.PI*2,scale-.03f,y0+v*.034f),(u,v)=>-Out(u*MathF.PI*2),(u,v)=>new Paint(deep,.7f,true));
            k.Tube(s,Around,6,t=>Band(t*MathF.PI*2,scale+.008f,y0),t=>.004f,(u,t)=>new Paint(deep,1,true),true);
            k.Tube(s,Around,6,t=>Band(t*MathF.PI*2,scale+.008f,y0+.034f),t=>.004f,(u,t)=>new Paint(deep,1,true),true);
            for(int i=0;i<5;i++)
            {
                float th=i*MathF.PI*2/5;var b0=Band(th-.22f,scale,y0+.034f);var b1=Band(th+.22f,scale,y0+.034f);var tip=Band(th,scale*.97f,y0+.034f+.052f);var n=Out(th);
                int a=k.Vertex(s,b0,n,new Paint(gold,1,true)),bb=k.Vertex(s,b1,n,new Paint(gold,1,true)),c=k.Vertex(s,tip,n,new Paint(gold,1.08f,true));Kit.Tri(s,a,bb,c);
                int a2=k.Vertex(s,b0-n*.004f,-n,new Paint(deep,.7f,true)),b2=k.Vertex(s,b1-n*.004f,-n,new Paint(deep,.7f,true)),c2=k.Vertex(s,tip-n*.004f,-n,new Paint(deep,.7f,true));Kit.Tri(s,a2,c2,b2);
                k.Ball(s,tip+Vector3.UnitY*.006f,.0075f,0,i,d=>new Paint(gold,1.1f,true),6,10);
                int jewel=i%2==0?0xe0304a:0x2a7bf0;k.Ball(s,Band(th,scale+.012f,y0+.017f),.0085f,0,i,d=>new Paint(jewel,1,false,.25f),6,10);
            }
        }

        // ---- cone: a party hat on the crown, a spiral stripe, a pom at its tip ----
        private const float ConeY0=BandY+.07f,ConeHeight=.2f,ConeScale=.68f;
        private static Vector3 ConeShell(float u,float v){var b=Band(u*MathF.PI*2,ConeScale*(1-v),ConeY0+ConeHeight*v);b.Z+=.01f*v;return b;}
        private static void Cone(Kit k)
        {
            int main=0xff6b35,stripe=0xfff1c2;var s=k.Part("cone");const float y0=ConeY0,h=ConeHeight,scale=ConeScale;
            k.Surface(s,Around,16,true,ConeShell,(u,v)=>Out(u*MathF.PI*2)+Vector3.UnitY*.4f,
                (u,v)=>{float th=u*MathF.PI*2,sp=MathF.Sin(th*2+v*14);return sp>.55f?new Paint(stripe,1):new Paint(main,1,true);});
            k.Surface(s,Around,1,true,(u,v)=>Band(u*MathF.PI*2,scale,y0+v*.002f),(u,v)=>-Vector3.UnitY,(u,v)=>new Paint(main,.5f,true));
            k.Ball(s,new Vector3(0,y0+h+.012f,BandZ+.01f),.02f,.18f,9,d=>new Paint(stripe,1.05f),10,16);
            // The elastic under the chin: a thin cord from each side down under the jaw.
            foreach(float side in new[]{-1f,1f})k.Tube(s,12,5,t=>{var a=Band(side*MathF.PI/2,scale,y0);var b=new Vector3(side*.07f,-.045f,.075f);var mid=(a+b)*.5f+new Vector3(side*.035f,0,0);return Vector3.Lerp(Vector3.Lerp(a,mid,t),Vector3.Lerp(mid,b,t),t);},t=>.0016f,(u,t)=>new Paint(0xe8e8ec,.9f));
        }

        // ---- antenna: a headband, two coiled stalks, glowing balls ----
        private static void Antenna(Kit k)
        {
            int main=0xff4949,metal=0x9ca3af;var s=k.Part("antenna");
            // The headband over the crown from ear to ear (over the spiky hair's root).
            Vector3 Arc(float t){float a=(t-.5f)*MathF.PI*1.08f;return new Vector3(.128f*MathF.Sin(a),.07f+.165f*MathF.Cos(a),BandZ-.006f);}
            k.Tube(s,32,10,Arc,t=>.0065f,(u,t)=>new Paint(0x2a2f3d,1));
            foreach(float side in new[]{-1f,1f})
            {
                var root=Arc(.5f+side*.17f);k.Ball(s,root,.011f,0,2,d=>new Paint(metal,.9f),6,10);
                // A coiled stalk leaning out, its coil narrowing.
                Vector3 Stalk(float t){float turn=t*MathF.PI*2*5,rad=.006f*(1-.6f*t);var up=root+new Vector3(side*.03f*t,.09f*t,-.012f*t);return up+new Vector3(MathF.Cos(turn)*rad,0,MathF.Sin(turn)*rad);}
                k.Tube(s,80,6,Stalk,t=>.0022f,(u,t)=>new Paint(metal,1.05f));
                k.Ball(s,Stalk(1)+new Vector3(0,.016f,0),.017f,.03f,4,d=>new Paint(main,1.1f,true,.9f),10,16);
            }
        }

        // ---- halo: a glowing ring over the hair ----
        private static void Halo(Kit k)
        {
            int main=0xffd971;var s=k.Part("halo");float y=.33f;
            k.Tube(s,Around,12,t=>{var b=Band(t*MathF.PI*2,.9f,y);b.Y+=.01f*MathF.Cos(t*MathF.PI*2);return b;},t=>.0085f,(u,t)=>new Paint(main,1,true,1.4f),true);
        }

        // ---- cowboy: a pinched crown creased on top, a wide brim rolled at the sides, a band with a buckle ----
        private static Vector3 CowboyCrown(float u,float v)
        {
            float th=u*MathF.PI*2;var p=Dome(u,v,BandY-.004f,CrownTop+.035f,.04f,.01f,.55f);
            // The crease down the top front to back (along the midline, so the crown meets itself at its peak) and the pinches either
            // side of the front (gone at the peak).
            float top=Smooth((v-.55f)/.45f);p.Y-=.028f*top*MathF.Exp(-p.X*p.X/(.042f*.042f));
            float pinch=MathF.Exp(-MathF.Pow((Near(th,0)-.5f)/.22f,2))*Smooth((v-.4f)/.5f)*Smooth((1-v)/.3f);p-=Out(th)*.016f*pinch;return p;
        }
        private static void Cowboy(Kit k)
        {
            int main=0xbc8352,dark=0x8e5938,band=0x3b2a1e,buckle=0xd8c27a;var s=k.Part("cowboy");float y0=BandY;
            k.Surface(s,Around,16,true,CowboyCrown,(u,v)=>DomeOut(u,v,y0),(u,v)=>new Paint(main,.95f+.05f*v,true));
            k.Surface(s,Around,2,true,(u,v)=>Band(u*MathF.PI*2,1.012f,y0+.002f+v*.024f),(u,v)=>Out(u*MathF.PI*2),(u,v)=>new Paint(band,.9f+.1f*v));
            k.Ball(s,Band(0,1.03f,y0+.014f),.009f,0,2,d=>new Paint(buckle,1,false,.15f),6,10);
            k.Slab(s,Around,8,true,.007f,(u,v)=>{float th=u*MathF.PI*2,side=MathF.Pow(Math.Abs(MathF.Sin(th)),1.6f);var p=Band(th,.98f+.78f*v,y0-.002f);p.Y+=.05f*side*v*v-.012f*(1-side)*v*v;return p;},
                (u,v)=>Vector3.UnitY,(u,v)=>new Paint(main,.92f,true),(u,v)=>new Paint(dark,1,true));
        }
    }
}
