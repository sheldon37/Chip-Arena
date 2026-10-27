using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One part of the Sheldon Head in the source frame (three.js, right-handed, +x the rig's left; front faces
    /// counter-clockwise seen from their normal). Colours and emission are linear; emission.w is the bank's tint tag (0 fixed,
    /// 1 the body tint: the glow colour). The Runtime mirrors x and turns the winding over once, as the bank export does.</summary>
    public sealed class SheldonSection
    {
        public string Part;public int Bone;public bool Transparent,DoubleSide;
        public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
        public readonly List<Vector4> Colors=new List<Vector4>(),Emission=new List<Vector4>();public readonly List<int> Indices=new List<int>();
        public int Add(Vector3 p,Vector3 n,Vector4 c,Vector4 e){Positions.Add(p);Normals.Add(n);Colors.Add(c);Emission.Add(e);return Positions.Count-1;}
        public void Tri(int a,int b,int c){Indices.Add(a);Indices.Add(b);Indices.Add(c);}
        public void Quad(int a,int b,int c,int d){Tri(a,b,c);Tri(a,c,d);}
    }

    /// <summary>The Sheldon Head's expression at one moment (NativeSheldonHead.Evaluate).</summary>
    public struct SheldonFace
    {
        /// <summary>Eye height (1 open crescents, ~.1 mid-blink), the eyes' lift (m), the grin's width scale, the jaw's opening
        /// (0 closed .. 1 laughing), the grin's sideways jitter (m), the glow's brightness and the ring's turn (rad); RingGrow
        /// scales the ring up from rest (0: rest; the Sheldon Intro's snap-on, NativeSheldonIntro.Face).</summary>
        public float EyeOpen,EyeLift,GrinWide,JawOpen,Jitter,Glow,RingSpin,RingGrow;
        public static SheldonFace Rest=>new SheldonFace{EyeOpen=1,GrinWide=1,Glow=1};
    }

    /// <summary>
    /// The Sheldon Head (DEV hat, owner 2026-10-12: "it will give my player this glowing face, along with changing the head +
    /// shoulder area if needed to give me a hooded appearance"), built procedurally from the owner's reference: a dark voxel
    /// hood enveloping the head and draping onto the shoulders and upper chest, its face opening a deep black void with a
    /// glowing face in it (two happy upturned crescent eyes and a wide grin of jagged interlocking teeth), a broken glowing
    /// ring behind the head, glowing trim along the hood's opening and the jacket's V, a small chevron on the chest and a ring
    /// on the right shoulder. Every glow carries the body tint tag, so the pilot's hat colour recolours it (cyan on Auto);
    /// the hood stays dark.
    /// Parts ride the rig's joints: the hood, void, face and ring on the head (so the hood stays on a severed head), the
    /// mantle on the chest and a drape on each clavicle (so raised arms carry their shoulder). The face and ring are separate
    /// pieces the live arena and portraits animate (Evaluate, Piece); everywhere else they show at rest in the bank model.
    /// Build is deterministic (no Random, fixed iteration order) and allocates only its result.
    /// </summary>
    public static class NativeSheldonHead
    {
        public const string Hat=NativeHatLooks.SheldonHead;
        public const int Hood=0,Void=1,EyeLeft=2,EyeRight=3,GrinUpper=4,GrinLower=5,Ring=6,RingGlow=7,Mantle=8,DrapeLeft=9,DrapeRight=10,Count=11;
        public static readonly string[] Parts={"sheldon-hood","sheldon-void","sheldon-eye-left","sheldon-eye-right","sheldon-grin-upper","sheldon-grin-lower","sheldon-ring","sheldon-ring-glow","sheldon-mantle","sheldon-drape-left","sheldon-drape-right"};
        /// <summary>The pieces the live arena and the portraits animate (the face and the ring); the rest is the hood's shell.</summary>
        public static bool Animated(int part)=>part>=EyeLeft&&part<=RingGlow;
        /// <summary>The whole hat's budget (all parts), and the highest it reaches above the head joint (the ring's top).</summary>
        public const int MaxVertices=8192,MaxTriangles=8192;
        public static float Top=>RingCentre.Y+RingRadius+RingWidth;

        // ---- Shape (head space unless noted; metres) ----------------------------------------------------------------------
        /// <summary>The hood's voxel and the head it clears (blank-head, crown and jaw of the pilot rig, plus a margin).</summary>
        public const float Voxel=.032f;
        public const float ClearX=.186f,ClearBottom=-.055f,ClearTop=.38f,ClearBack=-.186f,ClearFront=.178f;
        /// <summary>The face opening: .162 wide each side, narrowing a little toward the neck; above .165 a round arch (as if
        /// .215 wide) rising .185, which a voxel further out each side (EyeBottom..EyeTop, cut from EyeDepth forward) widens
        /// it round the eyes.</summary>
        public const float OpenHalf=.162f,OpenRound=.165f,OpenRise=.185f,OpenDepth=.07f,OpenPower=2.4f,ArchHalf=.215f,EyeBottom=.176f,EyeTop=.24f,EyeDepth=.14f;
        /// <summary>The void plate just in front of the head's face, and the face drawn just in front of it.</summary>
        public const float PlateZ=.171f,FaceZ=.1755f,PlateHalf=.1755f,PlateBottom=-.047f,PlateTop=.366f,PlateSide=.2075f;
        /// <summary>The eyes: centres, crescent radius, flattening, thickness and tilt (rad; the outer ends dip).</summary>
        public const float EyeX=.068f,EyeY=.212f,EyeR=.052f,EyeFlat=.92f,EyeThick=.032f,EyeTilt=.14f;
        /// <summary>The grin: half width, the upper lip's centre and curve, the lower lip's drop, the teeth and the gap between the rows.</summary>
        public const float GrinW=.136f,GrinTop=.118f,GrinCurve=1.9f,GrinDrop=.098f,ToothReach=.78f,ToothGap=.015f;public const int Teeth=7;
        /// <summary>The ring behind the head: centre, radius, ribbon width and depth, and its glow ribbon's width.</summary>
        public static readonly Vector3 RingCentre=new Vector3(0,.215f,-.33f);
        public const float RingRadius=.335f,RingWidth=.024f,RingDepth=.012f,RingGlowWidth=.09f;
        /// <summary>The ring's broken arcs (degrees from +x, counter-clockwise seen from the front), long sweeps and short dashes.</summary>
        public static readonly float[] RingArcs={8,52, 60,74, 82,148, 156,163, 171,226, 236,246, 255,300, 309,316, 324,352};

        // ---- Colours (sRGB hex for the fixed ones; the glow is the body tint times these levels) ---------------------------
        public const int Fabric=0x161a26,FabricTop=0x1c2132,Inner=0x06070b,PlateColor=0x020203;
        public const float TrimLevel=.55f,TrimGlow=1.15f,FaceLevel=.6f,FaceGlow=1.75f,RingLevel=.6f,RingGlow2=1.3f,HaloAlpha=.32f,HaloGlow=.9f;
        /// <summary>The trim: thin glowing strips along the voxel edges of the opening (a pixel-stepped piping), lifted off the cloth.</summary>
        public const float StripWidth=.0075f,StripLift=.0012f,TrimBottom=-.02f,TrimDepth=PlateZ-Voxel/2;

        private static Vector4 Lin(int hex,float a=1){var c=NativePortraitRules.Linear(hex);return new Vector4(c.X,c.Y,c.Z,a);}
        private static Vector4 Glow(float level){return new Vector4(level,level,level,1);}
        private static readonly Vector4 NoGlow=Vector4.Zero;

        // ---- Voxels -----------------------------------------------------------------------------------------------------
        private sealed class Grid
        {
            public readonly int[] N;public readonly Vector3 Min;public readonly byte[] Cells;
            public Grid(Vector3 min,int nx,int ny,int nz){Min=min;N=new[]{nx,ny,nz};Cells=new byte[nx*ny*nz];}
            public byte Get(int x,int y,int z)=>x<0||y<0||z<0||x>=N[0]||y>=N[1]||z>=N[2]?(byte)0:Cells[(z*N[1]+y)*N[0]+x];
            public void Set(int x,int y,int z,byte v)=>Cells[(z*N[1]+y)*N[0]+x]=v;
            public Vector3 Centre(int x,int y,int z)=>Min+new Vector3((x+.5f)*Voxel,(y+.5f)*Voxel,(z+.5f)*Voxel);
        }
        private const byte Cloth=1;
        // A thin glowing strip on the face of the cell at `centre` facing `n`, along that face's edge on side `side` (both unit
        // axes), StripWidth wide, lifted off the face and running half a strip past each end so steps join. `place` mirrors.
        private static void EdgeStrip(SheldonSection s,Vector3 centre,Vector3 n,Vector3 side,Func<Vector3,Vector3> place=null)
        {
            var along=Vector3.Cross(n,side);var edge=centre+n*(Voxel/2+StripLift)+side*(Voxel/2);float half=Voxel/2+StripWidth*.5f;
            Vector3 P(Vector3 v)=>place!=null?place(v):v;var nn=place!=null?Vector3.Normalize(place(n)-place(Vector3.Zero)):n;
            var col=Glow(TrimLevel);var emi=Glow(TrimGlow);
            int a=s.Add(P(edge-along*half),nn,col,emi),b=s.Add(P(edge+along*half),nn,col,emi),c=s.Add(P(edge+along*half-side*StripWidth),nn,col,emi),d=s.Add(P(edge-along*half-side*StripWidth),nn,col,emi);
            Facing(s,a,b,c,d,nn);
        }
        private static readonly Vector3[] InPlane={Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY};
        // Exposed faces merged into rectangles of equal key (greedy meshing), per axis and side. `key` (0: no face) for the face
        // of a filled cell toward an empty neighbour; `look` colours one merged quad from its key, centre and normal.
        private static void Greedy(Grid g,SheldonSection s,Func<int,int,int,int,int,int> key,Func<int,Vector3,Vector3,(Vector4,Vector4)> look,Func<Vector3,Vector3> place=null)
        {
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3,nu=g.N[u],nv=g.N[v];var mask=new int[nu*nv];var c=new int[3];
                var normal=Vector3.Zero;if(axis==0)normal.X=sign;else if(axis==1)normal.Y=sign;else normal.Z=sign;
                for(int d=0;d<g.N[axis];d++)
                {
                    for(int j=0;j<nv;j++)for(int i=0;i<nu;i++)
                    {
                        c[axis]=d;c[u]=i;c[v]=j;int k=0;
                        if(g.Get(c[0],c[1],c[2])!=0){c[axis]+=sign;bool open=g.Get(c[0],c[1],c[2])==0;c[axis]-=sign;if(open)k=key(c[0],c[1],c[2],axis,sign);}
                        mask[j*nu+i]=k;
                    }
                    for(int j=0;j<nv;j++)for(int i=0;i<nu;)
                    {
                        int k=mask[j*nu+i];if(k==0){i++;continue;}
                        int w=1;while(i+w<nu&&mask[j*nu+i+w]==k)w++;
                        int h=1;for(bool ok=true;ok&&j+h<nv;){for(int q=0;q<w;q++)if(mask[(j+h)*nu+i+q]!=k){ok=false;break;}if(ok)h++;}
                        for(int b=0;b<h;b++)for(int q=0;q<w;q++)mask[(j+b)*nu+i+q]=0;
                        float plane=Comp(g.Min,axis)+(d+(sign>0?1:0))*Voxel,u0=Comp(g.Min,u)+i*Voxel,u1=u0+w*Voxel,v0=Comp(g.Min,v)+j*Voxel,v1=v0+h*Voxel;
                        Vector3 P(float uu,float vv){var p=new float[3];p[axis]=plane;p[u]=uu;p[v]=vv;var r=new Vector3(p[0],p[1],p[2]);return place!=null?place(r):r;}
                        var n=place!=null?Vector3.Normalize(place(normal)-place(Vector3.Zero)):normal;var (col,emi)=look(k,(P(u0,v0)+P(u1,v1))/2,n);
                        int a=s.Add(P(u0,v0),n,col,emi),b1=s.Add(P(u1,v0),n,col,emi),c1=s.Add(P(u1,v1),n,col,emi),d1=s.Add(P(u0,v1),n,col,emi);
                        if(sign>0)s.Quad(a,b1,c1,d1);else s.Quad(a,d1,c1,b1);
                        i+=w;
                    }
                }
            }
        }
        // A quad turned to face `n` (counter-clockwise seen from that side).
        private static void Facing(SheldonSection s,int a,int b,int c,int d,Vector3 n)
        {var cr=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a])+Vector3.Cross(s.Positions[c]-s.Positions[a],s.Positions[d]-s.Positions[a]);if(Vector3.Dot(cr,n)>=0)s.Quad(a,b,c,d);else s.Quad(a,d,c,b);}
        private static float Comp(Vector3 v,int axis)=>axis==0?v.X:axis==1?v.Y:v.Z;
        private static float Hash(float x,float y,float z){double s=Math.Sin(x*127.1+y*311.7+z*74.7)*43758.5453;return (float)(s-Math.Floor(s));}
        // Cloth shading per merged quad: a faint patchwork (each rectangle a touch lighter or darker), tops a little lighter.
        private static Vector4 Cloth4(Vector3 centre,Vector3 n,bool inner)
        {
            if(inner)return Lin(Inner);var c=Lin(n.Y>.5f?FabricTop:Fabric);float k=.86f+.26f*Hash(centre.X,centre.Y,centre.Z);return new Vector4(c.X*k,c.Y*k,c.Z*k,1);
        }

        // ---- Hood (head joint) --------------------------------------------------------------------------------------------
        private static bool InOpening(float x,float y)
        {
            float ax=Math.Abs(x);
            if(y>=OpenRound){float a=ax/ArchHalf,b=(y-OpenRound)/OpenRise;return MathF.Pow(a,OpenPower)+MathF.Pow(b,OpenPower)<1&&(ax<OpenHalf||y>EyeBottom&&y<EyeTop);}
            float narrow=OpenHalf-Math.Max(0,Math.Min(1,-y/.16f))*.032f;return ax<narrow;
        }
        // The eye-level widening is cut from EyeDepth forward only: behind it the shell still closes the side of the head.
        private static bool HoodOpen(Vector3 p)=>p.Z>(Math.Abs(p.X)<OpenHalf?OpenDepth:EyeDepth)&&InOpening(p.X,p.Y);
        // The hood: one voxel of cloth round the head's clearance (face and edge cells, the corners left out so it rounds off),
        // ending under the jaw (the mantle's collar covers the neck, so a turning head carries no cowl into swings that pass
        // close in front of the chest and shoulders). A brim and front edges reach forward past the face, so the face sits deep
        // in shadow; above the eyes the brim reaches a voxel further (Brow). Owner 2026-10-12 ("widen the hood slightly (in
        // reference to the picture) But do NOT expand the hitbox"): roomier round the head, a voxel (Cowl) further out where
        // nothing the pilot holds passes. Behind the ears and over the crown it balloons out, standing off the head (the
        // shell's side column there left open as a pocket), the crown is a voxel fuller and the peak taller and leaning back;
        // at the eyes the opening widens a voxel each side inside a rim a voxel out. The cheeks and jaw in front of the ears
        // stay tight (the rifle and machine gun stocks and two-handed grips pass there), as does the back (greatsword
        // windups), and the hood still ends under the jaw: a lower hem, a wider mantle or a cheek flare all put cloth into
        // swings, aims and lobby poses (Tests/HatLookChecks' clearance; DEV_LOG). Cloth only: hits never read it.
        public const float HoodBottom=-.085f;
        /// <summary>The cowl: its outer half width (a voxel past the shell); the balloon behind the ears (BalloonBottom..Top,
        /// back to BalloonBack, forward to BalloonFront below BalloonStep and BalloonFrontHigh above it); the crown's shoulder
        /// over the clearance (CrownBack..ShoulderFront); the eye rim's front; the brow's reach (from BrowBottom, within BrowHalf).</summary>
        public const float CowlHalf=.24f,BalloonBottom=-.016f,BalloonTop=.368f,BalloonBack=-.176f,BalloonFront=-.08f,BalloonStep=.144f,BalloonFrontHigh=-.048f,ShoulderFront=-.016f,RimFront=.24f,BrowBottom=.27f,BrowTop=.4f,BrowHalf=.18f,BrowFront=.272f;
        /// <summary>The crown's extra layers (half width, back and front for the first and second) and the peak (base half
        /// width, top, front and back at its base, and the lean of both per metre of rise).</summary>
        public const float CrownHalf=.17f,CrownBack=-.208f,CrownFront=.08f,Crown2Half=.13f,Crown2Back=-.176f,Crown2Front=.016f,PeakHalf=.14f,PeakTop=.56f,PeakFront=.02f,PeakBack=-.2f,PeakLean=.4f;
        private static bool HoodCloth(Vector3 p)
        {
            float x=p.X,y=p.Y,z=p.Z,ax=Math.Abs(x),e=Voxel+1e-4f;if(y<HoodBottom||y>PeakTop)return false;
            float dx=Math.Max(0,ax-ClearX),dy=y>ClearTop?y-ClearTop:y<ClearBottom?ClearBottom-y:0,dz=z>ClearFront?z-ClearFront:z<ClearBack?ClearBack-z:0;
            if(dx==0&&dy==0&&dz==0)return false;
            bool side=dx>0&&dx<=e,outer=ax>ClearX+e&&ax<CowlHalf;
            bool shell=dx<=e&&dy<=e&&dz<=e&&!(dx>0&&dy>0&&dz>0);
            bool brim=y>.3f&&y<.42f&&z>.1f&&z<.245f&&ax<.21f*(float)Math.Sqrt(Math.Max(0,1-Math.Pow((y-.36f)/.075f,2)));
            bool edges=y>HoodBottom&&y<.36f&&z>.1f&&z<.235f&&ax<.214f;
            bool brow=y>BrowBottom&&y<BrowTop&&z>ClearFront&&z<BrowFront&&ax<BrowHalf;
            bool balloon=y>BalloonBottom&&y<BalloonTop&&z>BalloonBack&&z<(y>BalloonStep?BalloonFrontHigh:BalloonFront);
            bool cowl=outer&&(balloon||y>ClearTop&&y<ClearTop+Voxel&&z>CrownBack&&z<ShoulderFront||y>EyeBottom&&y<EyeTop&&z>EyeDepth&&z<RimFront);
            bool crown=y>ClearTop+Voxel/2&&y<ClearTop+Voxel*1.5f&&z>CrownBack&&z<CrownFront&&ax<CrownHalf||y>ClearTop+Voxel*1.5f&&y<ClearTop+Voxel*2.5f&&z>Crown2Back&&z<Crown2Front&&ax<Crown2Half;
            bool peak=y>.4f&&z>PeakBack+(y-.4f)*PeakLean&&z<PeakFront-(y-.4f)*PeakLean&&ax<PeakHalf*(1-(y-.4f)/.16f);
            if(!(shell||brim||edges||brow||cowl||crown||peak))return false;
            // Inside the balloon the shell's side column is left open: the cowl stands off the head there.
            if(side&&dy==0&&balloon&&z<BalloonFront)return false;
            // Under the jaw the hood is open round the neck.
            if(y<=ClearBottom&&x*x+z*z<.15f*.15f)return false;
            return !HoodOpen(p);
        }
        private static readonly Vector3 HeadCentre=new Vector3(0,.17f,-.005f),FaceCentre=new Vector3(0,.18f,.18f);
        private static void BuildHood(SheldonSection s)
        {
            var g=new Grid(HoodOrigin,21,23,21);
            for(int z=0;z<21;z++)for(int y=0;y<23;y++)for(int x=0;x<21;x++)if(HoodCloth(g.Centre(x,y,z)))g.Set(x,y,z,Cloth);
            Greedy(g,s,(x,y,z,axis,sign)=>
            {
                var c=g.Centre(x,y,z);var n=Vector3.Zero;if(axis==0)n.X=sign;else if(axis==1)n.Y=sign;else n.Z=sign;
                // Inside: facing the head, or looking into the opening (the eye rim's sides).
                bool inner=Vector3.Dot(n,c-HeadCentre)<0||HoodOpen(c+n*Voxel);int spill=0;
                if(inner&&c.Z>0){float d=Vector3.Distance(c,FaceCentre);spill=(int)Math.Round(Math.Max(0,1-d/.24f)*3);}
                return 10+(inner?100:0)+spill+(n.Y>.5f?50:0);
            },(k,centre,n)=>
            {
                bool inner=k>=110;int spill=(k-10)%50;if(inner)spill=(k-110)%50;
                var col=Cloth4(centre,n,inner);
                return spill>0?(col,new Vector4(.05f*spill,.05f*spill,.05f*spill,1)):(col,NoGlow);
            });
            // The trim: piping along the opening's rim on the hood's front faces.
            for(int z=0;z<21;z++)for(int y=0;y<23;y++)for(int x=0;x<21;x++)
            {
                if(g.Get(x,y,z)!=Cloth||g.Get(x,y,z+1)!=0)continue;var p=g.Centre(x,y,z);if(p.Z<TrimDepth||p.Y<TrimBottom)continue;
                for(int k=0;k<4;k++){int dx=k==0?1:k==1?-1:0,dy=k==2?1:k==3?-1:0;if(g.Get(x+dx,y+dy,z)==0&&HoodOpen(g.Centre(x+dx,y+dy,z)))EdgeStrip(s,p,Vector3.UnitZ,InPlane[k]);}
                // Where the rim steps back (the brow beside the shallower rim), the piping turns along the step's side face.
                for(int k=0;k<4;k++)for(int o=0;o<4;o++)
                {
                    if(k/2==o/2)continue;int dx=k==0?1:k==1?-1:0,dy=k==2?1:k==3?-1:0,ox=o==0?1:o==1?-1:0,oy=o==2?1:o==3?-1:0;
                    if(g.Get(x+dx,y+dy,z)!=0||HoodOpen(g.Centre(x+dx,y+dy,z))||g.Get(x+dx,y+dy,z-1)==0)continue;
                    if(g.Get(x+ox,y+oy,z)==0&&HoodOpen(g.Centre(x+ox,y+oy,z)))EdgeStrip(s,p,InPlane[k],InPlane[o]);
                }
            }
        }

        // ---- Void plate and face ---------------------------------------------------------------------------------------
        /// <summary>The upper lip, the lower lip and the zigzag between the rows at x (head space y).</summary>
        public static float LipTop(float x)=>GrinTop+GrinCurve*x*x;
        public static float LipBottom(float x)=>GrinTop-GrinDrop+(GrinCurve+GrinDrop/(GrinW*GrinW))*x*x;
        public static float MouthMid=>GrinTop-GrinDrop*.5f;
        // Point i of 4*Teeth+1 along the zigzag from the left corner: tooth tips alternate (upper row's tips down, lower's up).
        private static float ZigX(int i)=>-GrinW+i*(2*GrinW)/(4*Teeth);
        private static float Zig(int i)
        {
            float x=ZigX(i),top=LipTop(x),bottom=LipBottom(x),mid=(top+bottom)/2,half=(top-bottom)/2;int phase=i%4;
            // phase 0: between teeth on the lower row's tip... phase 1 and 3 halfway, phase 2 an upper tooth's tip.
            float amp=phase==2?-ToothReach:phase==0?ToothReach:0;return mid+amp*half;
        }
        private static void Ribbon(SheldonSection s,List<Vector2> a,List<Vector2> b,float z,Vector4 col,Vector4 emi)
        {
            var n=Vector3.UnitZ;int start=s.Positions.Count;
            for(int i=0;i<a.Count;i++){s.Add(new Vector3(a[i].X,a[i].Y,z),n,col,emi);s.Add(new Vector3(b[i].X,b[i].Y,z),n,col,emi);}
            // Counter-clockwise seen from +z whichever way the edges run (judged on the middle span, which is never degenerate).
            int m=start+(a.Count/2-1)*2;bool ccw=Vector3.Cross(s.Positions[m+3]-s.Positions[m+1],s.Positions[m+2]-s.Positions[m+1]).Z>=0;
            for(int i=0;i+1<a.Count;i++){int p=start+i*2;if(ccw)s.Quad(p+1,p+3,p+2,p);else s.Quad(p,p+2,p+3,p+1);}
        }
        private static void BuildGrin(SheldonSection upper,SheldonSection lower)
        {
            int n=4*Teeth;var top=new List<Vector2>();var zu=new List<Vector2>();var zl=new List<Vector2>();var bottom=new List<Vector2>();
            for(int i=0;i<=n;i++)
            {
                float x=ZigX(i),y=Zig(i),t=LipTop(x),b=LipBottom(x),gap=Math.Min(ToothGap*.5f,(t-b)*.25f);
                // The rows close to the corners: the gap fades so the grin ends in sharp points.
                float edge=Math.Min(1,Math.Min(i,n-i)/2f);gap*=edge;
                top.Add(new Vector2(x,t));zu.Add(new Vector2(x,Math.Min(t,y+gap)));zl.Add(new Vector2(x,Math.Max(b,y-gap)));bottom.Add(new Vector2(x,b));
            }
            Ribbon(upper,top,zu,FaceZ,Glow(FaceLevel),Glow(FaceGlow));Ribbon(lower,zl,bottom,FaceZ,Glow(FaceLevel),Glow(FaceGlow));
        }
        /// <summary>An eye's crescent outline at rest (outer arc then inner arc back), head space, side +1 the rig's left.</summary>
        public static void EyeOutline(int side,List<Vector2> outer,List<Vector2> inner)
        {
            const int k=18;float a0=.36f,a1=MathF.PI-.36f,tilt=-side*EyeTilt,c=MathF.Cos(tilt),sn=MathF.Sin(tilt);var centre=new Vector2(side*EyeX,EyeY);
            for(int i=0;i<=k;i++)
            {
                float a=a0+(a1-a0)*i/k,s=MathF.Pow(MathF.Max(0,MathF.Sin(MathF.PI*i/k)),.7f),t=EyeThick*s;
                // Both edges bow upward (a closed, smiling eye): the inner edge is the same arc drawn smaller, so the band is
                // thickest on top and tapers to points at the ends.
                var o=new Vector2(EyeR*MathF.Cos(a),EyeR*MathF.Sin(a)*EyeFlat);var inn=new Vector2((EyeR-t*.35f)*MathF.Cos(a),(EyeR-t)*MathF.Sin(a)*EyeFlat);
                outer.Add(centre+new Vector2(o.X*c-o.Y*sn,o.X*sn+o.Y*c));inner.Add(centre+new Vector2(inn.X*c-inn.Y*sn,inn.X*sn+inn.Y*c));
            }
        }
        private static void BuildEye(SheldonSection s,int side){var o=new List<Vector2>();var i=new List<Vector2>();EyeOutline(side,o,i);Ribbon(s,o,i,FaceZ,Glow(FaceLevel),Glow(FaceGlow));}
        private static void BuildVoid(SheldonSection s,List<Vector2> features)
        {
            const int nx=20,ny=24;int start=s.Positions.Count;var col=Lin(PlateColor);
            // A soft bloom baked round the face, so it glows on screens without bloom too (tinted with the face).
            void Vertex(float x,float y)=>s.Add(new Vector3(x,y,PlateZ),Vector3.UnitZ,col,VoidBloom(features,new Vector2(x,y)));
            for(int j=0;j<=ny;j++)for(int i=0;i<=nx;i++)Vertex(-PlateHalf+2*PlateHalf*i/nx,PlateBottom+(PlateTop-PlateBottom)*j/ny);
            for(int j=0;j<ny;j++)for(int i=0;i<nx;i++){int a=start+j*(nx+1)+i;s.Quad(a,a+1,a+nx+2,a+nx+1);}
            // Its sides where the opening widens round the eyes, out to the eye rim (just inside its cloth).
            foreach(int side in new[]{-1,1})
            {
                const int sx=3,sy=8;int b=s.Positions.Count;
                for(int j=0;j<=sy;j++)for(int i=0;i<=sx;i++)Vertex(side*(PlateHalf+(PlateSide-PlateHalf)*i/sx),EyeBottom+.0005f+(EyeTop-EyeBottom-.001f)*j/sy);
                for(int j=0;j<sy;j++)for(int i=0;i<sx;i++){int a=b+j*(sx+1)+i;Facing(s,a,a+1,a+sx+2,a+sx+1,Vector3.UnitZ);}
            }
        }

        // ---- Ring (head joint) ------------------------------------------------------------------------------------------
        private static Vector3 RingPoint(float deg,float r,float z){float a=deg*MathF.PI/180;return RingCentre+new Vector3(r*MathF.Cos(a),r*MathF.Sin(a),z);}
        private static void BuildRing(SheldonSection core,SheldonSection glow)
        {
            Vector4 col=Glow(RingLevel),emi=Glow(RingGlow2);float ri=RingRadius-RingWidth/2,ro=RingRadius+RingWidth/2,zf=RingDepth/2,zb=-RingDepth/2;
            for(int k=0;k+1<RingArcs.Length;k+=2)
            {
                float a0=RingArcs[k],a1=RingArcs[k+1];int n=Math.Max(2,(int)Math.Ceiling((a1-a0)/4));
                // Four faces along the arc: front (+z), back, outer and inner rims; then the two end caps.
                void Side(Func<float,Vector3> p,Func<float,Vector3> q,Func<float,Vector3> normal)
                {
                    int start=core.Positions.Count;for(int i=0;i<=n;i++){float a=a0+(a1-a0)*i/n;core.Add(p(a),normal(a),col,emi);core.Add(q(a),normal(a),col,emi);}
                    for(int i=0;i<n;i++){int b=start+i*2;Facing(core,b,b+1,b+3,b+2,normal(a0+(a1-a0)*(i+.5f)/n));}
                }
                Vector3 Radial(float a){float r=a*MathF.PI/180;return new Vector3(MathF.Cos(r),MathF.Sin(r),0);}
                Side(a=>RingPoint(a,ri,zf),a=>RingPoint(a,ro,zf),a=>Vector3.UnitZ);
                Side(a=>RingPoint(a,ro,zb),a=>RingPoint(a,ri,zb),a=>-Vector3.UnitZ);
                Side(a=>RingPoint(a,ro,zf),a=>RingPoint(a,ro,zb),Radial);
                Side(a=>RingPoint(a,ri,zb),a=>RingPoint(a,ri,zf),a=>-Radial(a));
                foreach(var (a,dir) in new[]{(a0,-1f),(a1,1f)})
                {
                    float r=a*MathF.PI/180;var tangent=new Vector3(-MathF.Sin(r),MathF.Cos(r),0)*dir;
                    int b=core.Add(RingPoint(a,ri,zf),tangent,col,emi),c=core.Add(RingPoint(a,ro,zf),tangent,col,emi),d=core.Add(RingPoint(a,ro,zb),tangent,col,emi),e=core.Add(RingPoint(a,ri,zb),tangent,col,emi);
                    Facing(core,b,c,d,e,tangent);
                }
                // The soft glow: a wider ribbon (both faces drawn), bright on the ring's line and clear at its edges and tips.
                int g0=glow.Positions.Count;var ge=Glow(HaloGlow);
                for(int i=0;i<=n;i++)
                {
                    float a=a0+(a1-a0)*i/n,tip=Math.Min(1,Math.Min(i,n-i)/1.5f);
                    glow.Add(RingPoint(a,RingRadius-RingGlowWidth/2,0),Vector3.UnitZ,new Vector4(1,1,1,0),ge);
                    glow.Add(RingPoint(a,RingRadius,0),Vector3.UnitZ,new Vector4(1,1,1,HaloAlpha*tip),ge);
                    glow.Add(RingPoint(a,RingRadius+RingGlowWidth/2,0),Vector3.UnitZ,new Vector4(1,1,1,0),ge);
                }
                for(int i=0;i<n;i++){int b=g0+i*3;Facing(glow,b,b+3,b+4,b+1,Vector3.UnitZ);Facing(glow,b+1,b+4,b+5,b+2,Vector3.UnitZ);}
            }
        }

        // ---- Mantle (chest joint) and drapes (clavicles) -----------------------------------------------------------------
        // The jacket's V: open over the chest's glowing centre stripe (x +-.0375), widening up to the hood's opening.
        private static bool MantleV(Vector3 p)=>p.Z>.04f&&Math.Abs(p.X)<Math.Max(.07f,.028f+(p.Y-.15f)*.42f);
        private static bool MantleCloth(Vector3 p)
        {
            float x=p.X,y=p.Y,z=p.Z;if(y<.15f||y>.39f)return false;
            float rx=.305f-(y-.15f)*.5f,rz=.212f-(y-.15f)*.12f,a=x/rx,c=z/rz;if(a*a+c*c>=1)return false;
            // Clear the chest armour, the collar and the neck (the pilot rig's chest boxes plus a margin).
            if(Math.Abs(x)<.28f&&Math.Abs(z)<.164f&&y<.285f)return false;
            if(Math.Abs(x)<.157f&&Math.Abs(z)<.127f&&y<.33f)return false;
            if(x*x+z*z<.102f*.102f)return false;
            // The front stops at the yoke: arms that cross the chest (two-handed grips, carries) pass in front of the armour
            // as before instead of through a layer of cloth.
            if(z>0&&y<.24f&&Math.Abs(x)<.21f)return false;
            return !MantleV(p);
        }
        /// <summary>The chest armour's front (CapturedRig's chest box), where the chevron sits when no cloth covers it.</summary>
        public const float ChestFront=.15f;
        /// <summary>The drapes' top above the clavicle joint: the shoulder cap's top (.05) plus .018.</summary>
        public const float DrapeTop=.068f;
        /// <summary>The chest emblem's centre (chest space) and size: a small chevron on the left breast of the mantle.</summary>
        public static readonly Vector3 EmblemAt=new Vector3(.12f,.15f,0);public const float EmblemSize=.05f;
        private static void BuildMantle(SheldonSection s)
        {
            var g=new Grid(MantleOrigin,20,8,14);
            for(int z=0;z<14;z++)for(int y=0;y<8;y++)for(int x=0;x<20;x++)if(MantleCloth(g.Centre(x,y,z)))g.Set(x,y,z,Cloth);
            var torso=new Vector3(0,.2f,0);
            Greedy(g,s,(x,y,z,axis,sign)=>
            {
                var c=g.Centre(x,y,z);var n=Vector3.Zero;if(axis==0)n.X=sign;else if(axis==1)n.Y=sign;else n.Z=sign;
                return 10+(Vector3.Dot(n,c-torso)<0?100:0)+(n.Y>.5f?50:0);
            },(k,centre,n)=>(Cloth4(centre,n,k>=110),NoGlow));
            // The trim: piping down both edges of the jacket's V.
            for(int z=0;z<14;z++)for(int y=0;y<8;y++)for(int x=0;x<20;x++)
            {
                if(g.Get(x,y,z)!=Cloth||g.Get(x,y,z+1)!=0)continue;var p=g.Centre(x,y,z);if(p.Z<.1f)continue;
                for(int k=0;k<2;k++){int dx=k==0?1:-1;if(g.Get(x+dx,y,z)==0&&MantleV(g.Centre(x+dx,y,z)))EdgeStrip(s,p,Vector3.UnitZ,InPlane[k]);}
            }
            // The chevron sits on the mantle's front face at its column.
            float front=ChestFront;for(int z=0;z<14;z++)for(int y=0;y<8;y++)for(int x=0;x<20;x++){var c=g.Centre(x,y,z);if(g.Get(x,y,z)!=0&&Math.Abs(c.X-EmblemAt.X)<Voxel*.6f&&Math.Abs(c.Y-EmblemAt.Y)<Voxel*.6f)front=Math.Max(front,c.Z+Voxel/2);}
            Chevron(s,new Vector3(EmblemAt.X,EmblemAt.Y,front+.0025f));
        }
        // A "Λ": two bars meeting at the top, flat on the surface facing +z.
        private static void Chevron(SheldonSection s,Vector3 at)
        {
            float h=EmblemSize,w=EmblemSize*.42f,bar=EmblemSize*.17f;var col=Glow(TrimLevel);var emi=Glow(TrimGlow);var n=Vector3.UnitZ;
            foreach(int side in new[]{-1,1})
            {
                var top=at+new Vector3(0,h/2,0);var foot=at+new Vector3(side*w,-h/2,0);var dir=Vector3.Normalize(foot-top);var across=new Vector3(-dir.Y,dir.X,0)*bar/2;
                int a=s.Add(top-across,n,col,emi),b=s.Add(top+across,n,col,emi),c=s.Add(foot+across,n,col,emi),d=s.Add(foot-across,n,col,emi);
                // Counter-clockwise seen from +z whichever way the bar leans.
                var cross=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a]);if(cross.Z>0)s.Quad(a,b,c,d);else s.Quad(a,d,c,b);
            }
        }
        private static bool DrapeCloth(Vector3 p)
        {
            // A pauldron of cloth over the shoulder cap (x -.03..16, y -.07..05, z +-.105 on the clavicle): one voxel over its
            // top, ending .018 above it, so a blade the melee clearance rests on the cap (MeleeClearance: cap + .02) never sinks
            // into the cloth; its outer side hangs to the cap's lower edge, not over the arm.
            float x=p.X,y=p.Y,z=p.Z;if(x<-.01f||x>.205f||y<-.085f||y>DrapeTop||Math.Abs(z)>.15f)return false;
            if(y<-.03f&&x<.15f)return false;
            if(y>.04f&&(x>.17f||Math.Abs(z)>.125f))return false;
            return !(x>-.042f&&x<.172f&&y>-.084f&&y<.05f&&Math.Abs(z)<.115f);
        }
        private static void BuildDrape(SheldonSection s,int side)
        {
            var g=new Grid(DrapeOrigin,10,8,10);
            for(int z=0;z<10;z++)for(int y=0;y<8;y++)for(int x=0;x<10;x++)if(DrapeCloth(g.Centre(x,y,z)))g.Set(x,y,z,Cloth);
            Func<Vector3,Vector3> place=side>0?null:(Func<Vector3,Vector3>)(v=>new Vector3(-v.X,v.Y,v.Z));
            var cap=new Vector3(.065f,-.02f,0);
            int start=s.Indices.Count;
            Greedy(g,s,(x,y,z,axis,sign)=>
            {
                var c=g.Centre(x,y,z);var n=Vector3.Zero;if(axis==0)n.X=sign;else if(axis==1)n.Y=sign;else n.Z=sign;
                return 10+(Vector3.Dot(n,c-cap)<0?100:0)+(n.Y>.5f?50:0);
            },(k,centre,n)=>(Cloth4(centre,n,k>=110),NoGlow),place);
            // Mirroring the right drape turns its faces inside out: turn the winding back.
            if(place!=null)for(int i=start;i+2<s.Indices.Count;i+=3){int t=s.Indices[i+1];s.Indices[i+1]=s.Indices[i+2];s.Indices[i+2]=t;}
            // The trim: piping along the drape's lower outer edge (Facing orients each strip after mirroring).
            for(int z=0;z<10;z++)for(int y=0;y<8;y++)for(int x=0;x<10;x++){var c=g.Centre(x,y,z);if(g.Get(x,y,z)!=0&&g.Get(x+1,y,z)==0&&g.Get(x,y-1,z)==0)EdgeStrip(s,c,Vector3.UnitX,-Vector3.UnitY,place);}
            if(side<0)
            {
                // The right shoulder's ring emblem on the drape's outer face.
                float outer=0,mid=0;int cells=0;for(int z=0;z<10;z++)for(int y=0;y<8;y++)for(int x=0;x<10;x++){var c=g.Centre(x,y,z);if(g.Get(x,y,z)!=0&&c.X>.17f&&Math.Abs(c.Z)<Voxel){outer=Math.Max(outer,c.X+Voxel/2);mid+=c.Y;cells++;}}
                Annulus(s,new Vector3(-(outer+.0025f),cells>0?mid/cells:0,0),-Vector3.UnitX,.03f,.0075f);
            }
        }
        // A flat ring facing `n` (here -x, the right shoulder's outside).
        private static void Annulus(SheldonSection s,Vector3 at,Vector3 n,float r,float w)
        {
            const int k=20;var col=Glow(TrimLevel);var emi=Glow(TrimGlow);var u=Vector3.UnitZ;var v=Vector3.Cross(n,u);int start=s.Positions.Count;
            for(int i=0;i<=k;i++){float a=i*MathF.PI*2/k;var dir=u*MathF.Cos(a)+v*MathF.Sin(a);s.Add(at+dir*(r-w/2),n,col,emi);s.Add(at+dir*(r+w/2),n,col,emi);}
            for(int i=0;i<k;i++){int b=start+i*2;int p=b,q=b+1,r1=b+3,t=b+2;var cross=Vector3.Cross(s.Positions[q]-s.Positions[p],s.Positions[r1]-s.Positions[p]);if(Vector3.Dot(cross,n)>0)s.Quad(p,q,r1,t);else s.Quad(p,t,r1,q);}
        }

        // ---- Solids (the voxel cloth by joint space, for clearance checks) -------------------------------------------------
        private static readonly Vector3 HoodOrigin=new Vector3(-.336f,-.176f,-.336f),MantleOrigin=new Vector3(-.32f,.144f,-.224f),DrapeOrigin=new Vector3(-.064f,DrapeTop-8*Voxel,-.16f);
        private static bool Solid(Vector3 p,Vector3 origin,int nx,int ny,int nz,Func<Vector3,bool> cloth)
        {
            var q=(p-origin)/Voxel;int x=(int)Math.Floor(q.X),y=(int)Math.Floor(q.Y),z=(int)Math.Floor(q.Z);if(x<0||y<0||z<0||x>=nx||y>=ny||z>=nz)return false;
            return cloth(origin+new Vector3((x+.5f)*Voxel,(y+.5f)*Voxel,(z+.5f)*Voxel));
        }
        /// <summary>Whether a point in head space lies in the hood's cloth.</summary>
        public static bool HoodSolid(Vector3 p)=>Solid(p,HoodOrigin,21,23,21,HoodCloth);
        /// <summary>Whether a point in chest space lies in the mantle's cloth.</summary>
        public static bool MantleSolid(Vector3 p)=>Solid(p,MantleOrigin,20,8,14,MantleCloth);
        /// <summary>Whether a point in a clavicle's space (side +1 left, -1 right) lies in that drape's cloth.</summary>
        public static bool DrapeSolid(Vector3 p,int side)=>Solid(side>0?p:new Vector3(-p.X,p.Y,p.Z),DrapeOrigin,10,8,10,DrapeCloth);

        // ---- The whole hat -------------------------------------------------------------------------------------------------
        /// <summary>Every part in Parts order (deterministic: the same arrays on every call and machine).</summary>
        public static SheldonSection[] Build()
        {
            var parts=new SheldonSection[Count];
            for(int i=0;i<Count;i++)parts[i]=new SheldonSection{Part=Parts[i],Bone=i>=Mantle?(i==Mantle?CapturedRig.Chest:i==DrapeLeft?CapturedRig.LClav:CapturedRig.RClav):CapturedRig.Head,Transparent=i==RingGlow,DoubleSide=i==RingGlow};
            BuildHood(parts[Hood]);
            BuildEye(parts[EyeLeft],1);BuildEye(parts[EyeRight],-1);BuildGrin(parts[GrinUpper],parts[GrinLower]);
            BuildVoid(parts[Void],Features(parts));
            BuildRing(parts[Ring],parts[RingGlow]);
            BuildMantle(parts[Mantle]);BuildDrape(parts[DrapeLeft],1);BuildDrape(parts[DrapeRight],-1);
            return parts;
        }
        // The face's points: every vertex and triangle centre of the eyes and the grin.
        private static List<Vector2> Features(SheldonSection[] parts)
        {
            var features=new List<Vector2>();foreach(int p in new[]{EyeLeft,EyeRight,GrinUpper,GrinLower})
            {
                var sec=parts[p];foreach(var v in sec.Positions)features.Add(new Vector2(v.X,v.Y));
                for(int i=0;i+2<sec.Indices.Count;i+=3){var c=(sec.Positions[sec.Indices[i]]+sec.Positions[sec.Indices[i+1]]+sec.Positions[sec.Indices[i+2]])/3;features.Add(new Vector2(c.X,c.Y));}
            }
            return features;
        }
        /// <summary>The face's points at rest (head space; the void's soft glow is measured from them), for the Sheldon hood's void
        /// on the model (NativeSheldonHood.Void).</summary>
        public static List<Vector2> FaceFeatures()
        {
            var parts=new SheldonSection[Count];foreach(int p in new[]{EyeLeft,EyeRight,GrinUpper,GrinLower})parts[p]=new SheldonSection();
            BuildEye(parts[EyeLeft],1);BuildEye(parts[EyeRight],-1);BuildGrin(parts[GrinUpper],parts[GrinLower]);
            return Features(parts);
        }
        /// <summary>The void's colour (linear) and its emission at a point (head space): a soft bloom baked round the face, tinted
        /// with it, so it glows on screens without bloom too.</summary>
        public static Vector4 VoidColor=>Lin(PlateColor);
        public static Vector4 VoidBloom(List<Vector2> features,Vector2 at)
        {
            float d=float.MaxValue;foreach(var f in features)d=Math.Min(d,Vector2.Distance(f,at));d=Math.Max(0,d-.004f);
            float glow=.45f*MathF.Exp(-d/.011f)+.035f*MathF.Exp(-d/.03f);return new Vector4(glow,glow,glow,1);
        }
        public static int Vertices(SheldonSection[] parts){int n=0;foreach(var p in parts)n+=p.Positions.Count;return n;}
        public static int Triangles(SheldonSection[] parts){int n=0;foreach(var p in parts)n+=p.Indices.Count/3;return n;}

        // ---- Expression -------------------------------------------------------------------------------------------------
        /// <summary>A blink lasts this long; the gaps between blinks run Blink..Blink+BlinkSpread seconds (hashed per wearer).</summary>
        public const float BlinkTime=.16f,BlinkGap=2.8f,BlinkSpread=2.6f,HitTime=.45f,KillTime=1.4f,RingRate=.22f,ReducedRate=.2f;
        /// <summary>
        /// The face at `time` (any presentation clock) for wearer `seed`: idle breathing and blinks, a flicker and twitch for
        /// HitTime after a hit (`hitAge`, NaN or negative for none), and a widening, stuttering laugh for KillTime after a kill
        /// (`killAge`). Reduced effects: no strobe (the hit dims softly), a calmer laugh and a slower ring.
        /// </summary>
        public static SheldonFace Evaluate(float time,int seed,float hitAge,float killAge,bool reduced)
        {
            var f=SheldonFace.Rest;float phase=Hash01(seed,3)*6.283f;
            f.Glow=1+.05f*MathF.Sin(time*3.4f+phase);f.GrinWide=1+.012f*MathF.Sin(time*1.3f+phase);
            f.EyeOpen=BlinkOpen(time,seed);
            if(hitAge>=0&&hitAge<HitTime)
            {
                float env=1-hitAge/HitTime;env*=env;
                if(reduced)f.Glow*=1-.35f*env;
                else{bool on=Hash01((int)Math.Floor(time*30)+seed*977,11)>.38f;f.Glow*=on?1+.3f*env:1-.6f*env;f.Jitter=.0035f*env*MathF.Sin(time*83+phase);}
                f.JawOpen+=.25f*env;
            }
            if(killAge>=0&&killAge<KillTime)
            {
                float k=Smooth(killAge/.18f)*(1-Smooth((killAge-KillTime*.6f)/(KillTime*.4f)));
                float stutter=reduced?.8f:.55f+.45f*MathF.Abs(MathF.Sin(killAge*MathF.PI*7.5f));
                f.GrinWide+=.17f*k;f.JawOpen+=.75f*k*stutter;f.EyeLift+=.007f*k;f.EyeOpen*=1-.3f*k;f.Glow*=1+.35f*k;
            }
            f.JawOpen=Math.Min(1.2f,f.JawOpen);f.RingSpin=time*RingRate*(reduced?ReducedRate:1);
            return f;
        }
        /// <summary>The eyes' height at `time`: open, squashing to .1 and back over BlinkTime at hashed moments.</summary>
        public static float BlinkOpen(float time,int seed)
        {
            // Blinks come every BlinkGap..BlinkGap+BlinkSpread seconds: find the cycle `time` falls in (whole cycles of the mean).
            float mean=BlinkGap+BlinkSpread/2;int cycle=(int)Math.Floor(time/mean);
            for(int c=cycle-1;c<=cycle+1;c++)
            {
                float at=c*mean+Hash01(c*31+seed,5)*(mean-BlinkTime),q=(time-at)/BlinkTime;
                if(q>=0&&q<=1)return 1-.9f*MathF.Sin(q*MathF.PI);
            }
            return 1;
        }
        /// <summary>How a face or ring piece moves this frame (source frame, head space): about `Pivot`, scaled (Sx, Sy), turned
        /// Spin about +z and moved by Offset. The bank model shows them at rest (scale 1, nothing else).</summary>
        public struct PieceMotion{public Vector3 Pivot,Offset;public float Sx,Sy,Spin;}
        public static PieceMotion Piece(int part,in SheldonFace f)
        {
            var m=new PieceMotion{Sx=1,Sy=1};
            switch(part)
            {
                case EyeLeft:case EyeRight:m.Pivot=new Vector3((part==EyeLeft?1:-1)*EyeX,EyeY,FaceZ);m.Sy=Math.Max(.08f,f.EyeOpen);m.Offset=new Vector3(f.Jitter*.5f,f.EyeLift,0);break;
                case GrinUpper:m.Pivot=new Vector3(0,MouthMid,FaceZ);m.Sx=f.GrinWide;m.Offset=new Vector3(f.Jitter,.004f*f.JawOpen,0);break;
                case GrinLower:m.Pivot=new Vector3(0,MouthMid,FaceZ);m.Sx=f.GrinWide;m.Sy=1+.25f*f.JawOpen;m.Offset=new Vector3(f.Jitter,-.02f*f.JawOpen,0);break;
                case Ring:case RingGlow:m.Pivot=RingCentre;m.Spin=f.RingSpin;m.Sx=m.Sy=1+f.RingGrow;break;
            }
            return m;
        }
        /// <summary>A piece's point moved by its motion (for checks and the offline renders).</summary>
        public static Vector3 Apply(in PieceMotion m,Vector3 p)
        {
            var d=p-m.Pivot;d=new Vector3(d.X*m.Sx,d.Y*m.Sy,d.Z);float c=MathF.Cos(m.Spin),s=MathF.Sin(m.Spin);d=new Vector3(d.X*c-d.Y*s,d.X*s+d.Y*c,d.Z);return m.Pivot+m.Offset+d;
        }
        public static float Hash01(int n,int salt){unchecked{uint h=(uint)n*0x9E3779B1u^(uint)salt*0x85EBCA77u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h>>8)*(1f/16777216f);}}
        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
    }
}
