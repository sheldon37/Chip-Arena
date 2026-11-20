using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's flights (owner, 2026-11-20: "a lot of unnatural staircasing"): the stairs, the sloped streets and the fire escapes
    /// drawn on their colliders' lines, not on their treads' flat tops. Every flight is Astra's MapRamp: a pilot stands on a closed
    /// wedge from its bottom up to the slope through its two ends (Runtime NativeCyberCityExitCollision), and its treads are boxes
    /// for the drawing (0.25 m steps on a stair, 0.5 m on a sloped street), each from the flight's bottom up to its own flat top.
    /// Drawn tread by tread, their sides made a sawtooth: a sloped street's retaining edge and coping stepped every 3-4 m, a stair's
    /// cheek stepped every tread, and a fire escape stood as solid stepped wedges in its host's brick, hanging from each landing.
    /// Now a flight's sides end on its line (a sloped street's surface; a stair's nosings, which its treads already reach):
    ///  - a sloped street: engineering brick under a sloped stone coping whose top is the street's own edge; and the walls beside it
    ///    run down under the street (they stopped at the treads' tops, leaving notches open above the setts);
    ///  - a stair: a stone cheek under a sloped coping along its nosings;
    ///  - a fire escape: iron. A black stringer along the nosings; under it the wedge, solid to a pilot, is caged by a grille of flat
    ///    bars 0.3 m apart (no pilot passes, the eye does), with a grating under it, the stair's own soffit behind them and the street
    ///    running on under the cage. Truly open flights need thinner colliders (the notes' meridian-staircasing-astra.md).
    /// The treads' tops and risers keep their granite and iron grating. Presentation only: never a collider, never sim data.
    /// </summary>
    public static partial class NativeMeridianDress
    {
        /// <summary>The stringer's or cheek coping's thickness (m), the grille's bars (their spacing, width and inset behind the face)
        /// and its foot rail's height. The coping or stringer is <see cref="Stringer"/> deep.</summary>
        public const float StringThick=.06f,BarStep=.3f,BarWidth=.04f,BarInset=.03f,BarRail=.06f;

        /// <summary>A flight's frame (a MapRamp): its low end, its run's direction and the direction across it, its run, slope and
        /// step, and the line its sides end on.</summary>
        public struct FlightLine
        {
            public V3 Low,Dir,Across;public float Run,Slope,Step,Width,Bottom;public bool Stair;
            /// <summary>How far along the run a point lies (m).</summary>
            public float D(V3 p)=>(p.x-Low.x)*Dir.x+(p.z-Low.z)*Dir.z;
            /// <summary>The line at d m along the run: a sloped street's surface; a stair's nosings, half a step over its slope (each
            /// tread's top meets it at the tread's front edge).</summary>
            public float Line(float d)=>Low.y+Slope*d+(Stair?Step*.5f:0);
        }
        public static FlightLine LineOf(MapRamp r)
        {
            var d=r.high-r.low;float run=(float)Math.Sqrt(d.x*d.x+d.z*d.z);var dir=run>1e-4f?new V3(d.x/run,0,d.z/run):new V3(1,0,0);
            int steps=Math.Max(1,r.blocks?.Length??1);
            return new FlightLine{Low=r.low,Dir=dir,Across=new V3(dir.z,0,-dir.x),Run=run,Slope=run>1e-4f?d.y/run:0,Step=d.y/steps,Width=r.width,Bottom=r.bottom,Stair=r.kind=="stair"};
        }
        public static bool IsFireEscape(string id)=>id!=null&&id.StartsWith("fire-escape-",StringComparison.Ordinal);

        /// <summary>A flight's tread, every face: its top and risers in the tread's finish, its sides ending on the flight's line, a fire
        /// escape's other faces caged.</summary>
        private static void DressTread(Builder b,int bi,MapRamp r)
        {
            var F=LineOf(r);bool fire=IsFireEscape(b.Blocks[bi].id);
            for(int f=0;f<6;f++)
            {
                var n=NormalOf(f);bool side=f!=2&&f!=3&&Math.Abs(n.x*F.Across.x+n.z*F.Across.z)>.5f,riser=f!=2&&f!=3&&Facing(r,f);
                if(!F.Stair)
                {
                    // a sloped street: its surface is the wedge's (DressSlope); its sides retaining walls under a coping on that surface
                    if(side)FlightSide(b,bi,f,F,NativeMeridianSurfaces.BrickBlue,NativeMeridianSurfaces.Coping,false,false);
                    else if(f!=2&&f!=3&&!riser)Surface(b,bi,f,NativeMeridianSurfaces.BrickBlue,0,1.5f,NativeMeridianSurfaces.Coping,.3f);
                    continue;
                }
                if(f==2||riser){Surface(b,bi,f,fire?NativeMeridianSurfaces.IronGrate:NativeMeridianSurfaces.Granite,0,f==2?1f:1.5f);continue;}
                if(side){FlightSide(b,bi,f,F,fire?-1:NativeMeridianSurfaces.Stone,fire?NativeMeridianSurfaces.IronBlack:NativeMeridianSurfaces.Coping,true,fire);continue;}
                if(fire)Cage(b,bi,f,F);
                else Surface(b,bi,f,NativeMeridianSurfaces.Stone,0,1.5f);
            }
        }

        /// <summary>A tread's side, its exposed parts (as <see cref="Surface"/> finds them) ending on the flight's line: under a band
        /// <see cref="Stringer"/> m deep (a coping, a stringer) the body (in `bodyFin`, or with `grille` a fire escape's bars). With
        /// `cheek` the band has a top along the line and an inner face over the treads (a closed string).</summary>
        private static void FlightSide(Builder b,int bi,int f,FlightLine F,int bodyFin,int bandFin,bool cheek,bool grille)
        {
            if(OutOfBounds(b,bi,f))return;
            Lines(b,bi,f,1.5f,out var us,out var vs,out var o,out var u,out var v);int nu=us.Length-1,nv=vs.Length-1;
            var e=Exposed(b,bi,f,us,vs,o,u,v);var n=NormalOf(f);int ob=bi*6;float boxBottom=b.S.B[ob+2],boxTop=b.S.B[ob+3];
            // the band along the line over [A, A+ru]: a coping or stringer (its foot may dip into a neighbour: unseen there); with a
            // cheek, its top along the line and its inner face over the treads (from under the tread's top up to the line)
            void Band(V3 A,V3 ru)
            {
                float tA=F.Line(F.D(A)),tB=F.Line(F.D(A+ru)),cA=tA-Stringer,cB=tB-Stringer;
                b.Quad(new V3(A.x,cA,A.z),ru+new V3(0,cB-cA,0),new V3(0,Stringer,0),bandFin,0,bi,boxBottom);
                if(!cheek)return;
                var inward=n*-StringThick;var tq0=new V3(A.x,tA,A.z);var tu=ru+new V3(0,tB-tA,0);
                if(Cross(tu,inward).y>0)b.Quad(tq0,tu,inward,bandFin,0,bi,boxBottom);else b.Quad(tq0+tu,tu*-1,inward,bandFin,0,bi,boxBottom);
                float lo=boxTop-.05f;var ib=A+ru+inward;ib.y=lo;
                Trap(b,ib,ru*-1,new V3(0,tB-lo,0),new V3(0,tA-lo,0),n*-1,bandFin,0,bi,boxBottom);
            }
            foreach(var (i0,j0,i1,j1) in Rects(e,nu,nv))
            {
                var A=o+u*us[i0];var ru=u*(us[i1]-us[i0]);float y0=boxBottom+(boxTop-boxBottom)*vs[j0],y1=boxBottom+(boxTop-boxBottom)*vs[j1];A.y=y0;
                // (its top: the tread's own; its foot: nothing seen under it, the flight's bottom or a floor)
                bool top=vs[j1]>.999f,foot=j0==0||!e[i0+(j0-1)*nu];
                float cA=F.Line(F.D(A))-Stringer,cB=F.Line(F.D(A+ru))-Stringer;
                // the body's top: the band's foot, or a lower rect's own top where that is lower (a neighbour hides above it)
                if(grille)Grille(b,bi,A,ru,n,F,y0,top?float.MaxValue:y1,cA,cB,top?.15f:0,foot,boxBottom);
                else Under(b,bi,A,ru,n,y0,top?float.MaxValue:y1,cA,cB,bodyFin,boxBottom);
                if(top)Band(A,ru);
            }
            // the band where the tread's own top is hidden but its line rises clear of what hides it (a neighbour's top between the
            // tread's flat top and the line: a wagon against a stair)
            for(int i=0;i<nu;i++)
            {
                if(e[i+(nv-1)*nu])continue;
                var A=o+u*us[i];A.y=boxBottom;var ru=u*(us[i+1]-us[i]);var m=A+ru*.5f;float t=F.Line(F.D(m));if(t<boxTop+.03f)continue;
                m.y=t-.04f;m=m+n*.05f;if(b.S.Inside(m.x,m.y,m.z,bi))continue;
                Band(A,ru);
            }
        }
        /// <summary>The region over a side's segment [A, A+ru] from y0 up to min(cap, the line from cA to cB), as rectangles and
        /// trapezoids (split where the cap meets the line, and where the top falls to y0).</summary>
        private static void Under(Builder b,int bi,V3 A,V3 ru,V3 n,float y0,float cap,float cA,float cB,int fin,float floor)
        {
            // the top's breaks along the segment: where the line crosses the cap
            float s1=1;if(cap<float.MaxValue&&(cA-cap)*(cB-cap)<0)s1=(cap-cA)/(cB-cA);
            Piece(b,bi,A,ru,n,y0,0,s1,Math.Min(cap,cA),Math.Min(cap,cA+(cB-cA)*s1),fin,floor);
            if(s1<1)Piece(b,bi,A,ru,n,y0,s1,1,Math.Min(cap,cA+(cB-cA)*s1),Math.Min(cap,cB),fin,floor);
        }
        private static void Piece(Builder b,int bi,V3 A,V3 ru,V3 n,float y0,float s0,float s1,float h0,float h1,int fin,float floor)
        {
            if(s1-s0<1e-4f||h0<=y0+.005f&&h1<=y0+.005f)return;
            // where the top falls under the foot, only the part over it
            if(h0<y0){float s=s0+(s1-s0)*(y0-h0)/(h1-h0);s0=s;h0=y0;}
            else if(h1<y0){float s=s0+(s1-s0)*(h0-y0)/(h0-h1);s1=s;h1=y0;}
            var O=A+ru*s0;O.y=y0;var U=ru*(s1-s0);
            if(Math.Abs(h1-h0)<1e-4f){b.Quad(O,U,new V3(0,h0-y0,0),fin,0,bi,floor);return;}
            Trap(b,O,U,new V3(0,h0-y0,0),new V3(0,h1-y0,0),n,fin,0,bi,floor);
        }
        /// <summary>A trapezoid surface (sides V at O and V1 at O+U), split along the chunk grid along U.</summary>
        private static void Trap(Builder b,V3 o,V3 u,V3 v,V3 v1,V3 n,int fin,int layer,int block,float floor)
        {
            if(v.Length<1e-4f&&v1.Length<1e-4f)return;
            float l=(float)Math.Sqrt(u.x*u.x+u.z*u.z);int su=l<=ChunkSize*.75f?1:(int)Math.Ceiling(l/(ChunkSize*.5f));
            for(int a=0;a<su;a++)
            {
                float s0=(float)a/su,s1=(float)(a+1)/su;var va=v+(v1-v)*s0;var vb=v+(v1-v)*s1;
                var q=new Quad{O=o+u*s0,U=u*(1f/su),V=va,V1=vb,Taper=true,N=n,Fin=fin,Layer=layer,Block=block,Floor=floor};
                var c=q.O+q.U*.5f+(va+vb)*.25f;q.Chunk=b.P.ChunkOf(c.x,c.z);b.P.Quads.Add(q);
            }
        }

        /// <summary>A fire escape's grille over a side's segment: flat iron bars every <see cref="BarStep"/> m along the flight (one
        /// pattern down the whole run), set <see cref="BarInset"/> m behind the face, from y0 up into the stringer (`tuck` m) or to the
        /// cap, and a foot rail where nothing is seen under the segment (the flight's bottom, or a floor).</summary>
        private static void Grille(Builder b,int bi,V3 A,V3 ru,V3 n,FlightLine F,float y0,float cap,float cA,float cB,float tuck,bool foot,float floor)
        {
            float len=ru.Length;if(len<1e-3f)return;var along=ru/len;float dA=F.D(A),dB=F.D(A+ru);if(Math.Abs(dB-dA)<1e-4f)return;
            // (a bar on a tread's boundary belongs to the tread after it: each tread takes [d0, d1) by a hair, so the two treads'
            // float ends never both drop it)
            float d0=Math.Min(dA,dB),d1=Math.Max(dA,dB);
            for(int m=(int)Math.Floor(d0/BarStep)-1;;m++)
            {
                float d=(m+.5f)*BarStep;if(d>=d1-1e-4f)break;if(d<d0-1e-4f)continue;
                float s=(d-dA)/(dB-dA);float top=Math.Min(cap,cA+(cB-cA)*s+tuck);if(top<=y0+.02f)continue;
                var c=A+ru*s;c.y=y0;Bar(b,bi,c,new V3(0,top-y0,0),along,n,floor);
            }
            if(!foot)return;
            // the foot rail over the bars' feet (half their inset behind the face: off the floor's edge on the face's line)
            var r0=A;r0.y=y0;Strip(b,bi,r0,ru,new V3(0,BarRail,0),n,BarInset*.5f,floor);
        }
        /// <summary>A flat iron bar from c along `len`, <see cref="BarWidth"/> wide along `wide`, <see cref="BarInset"/> behind a face
        /// turned to n, facing out (from the cage's inside, unseen, a far side's bars give way to the view through).</summary>
        private static void Bar(Builder b,int bi,V3 c,V3 len,V3 wide,V3 n,float floor)=>Strip(b,bi,c-wide*(BarWidth*.5f),wide*BarWidth,len,n,BarInset,floor,true);
        /// <summary>An iron strip (o, u, v) set `inset` m behind a face turned to n, wound to face n; `free` out of the seam table (a
        /// bar meets no other surface edge to edge; a foot rail meets the next tread's end to end, so it keeps its seams).</summary>
        private static void Strip(Builder b,int bi,V3 o,V3 u,V3 v,V3 n,float inset,float floor,bool free=false)
        {
            o=o-n*inset;if(V3.Dot(Cross(u,v),n)<0){o=o+u;u=u*-1;}
            var q=new Quad{O=o,U=u,V=v,N=Cross(u,v).Normalized,Fin=NativeMeridianSurfaces.IronBlack,Block=bi,Floor=floor,Free=free};
            var c=q.O+q.U*.5f+q.V*.5f;q.Chunk=b.P.ChunkOf(c.x,c.z);b.P.Quads.Add(q);
        }

        /// <summary>A fire escape tread's other faces, where <see cref="Surface"/> finds them seen: under the flight a grating (bars
        /// across it in the flight's pattern along the run), at its high end (under the landing) bars up it across the flight.</summary>
        private static void Cage(Builder b,int bi,int f,FlightLine F)
        {
            if(OutOfBounds(b,bi,f))return;
            Lines(b,bi,f,1.5f,out var us,out var vs,out var o,out var u,out var v);int nu=us.Length-1,nv=vs.Length-1;
            var e=Exposed(b,bi,f,us,vs,o,u,v);var n=NormalOf(f);float floor=b.S.B[bi*6+2];
            foreach(var (i0,j0,i1,j1) in Rects(e,nu,nv))
            {
                var ro=o+u*us[i0]+v*vs[j0];var ru=u*(us[i1]-us[i0]);var rv=v*(vs[j1]-vs[j0]);
                if(f==3)
                {
                    bool uRun=Math.Abs(V3.Dot(ru,F.Dir))>=Math.Abs(V3.Dot(rv,F.Dir));var runE=uRun?ru:rv;var crossE=uRun?rv:ru;
                    float dS=F.D(ro),dE=F.D(ro+runE);if(Math.Abs(dE-dS)<1e-4f)continue;float d0=Math.Min(dS,dE),d1=Math.Max(dS,dE);
                    for(int m=(int)Math.Floor(d0/BarStep)-1;;m++){float d=(m+.5f)*BarStep;if(d>=d1-1e-4f)break;if(d<d0-1e-4f)continue;Bar(b,bi,ro+runE*((d-dS)/(dE-dS)),crossE,F.Dir,n,floor);}
                    continue;
                }
                bool uUp=Math.Abs(ru.y)>Math.Abs(rv.y);var upE=uUp?ru:rv;var acrossE=uUp?rv:ru;float len=acrossE.Length;if(len<1e-3f)continue;var cd=acrossE/len;
                for(int m=0;;m++){float a=(m+.5f)*BarStep;if(a>=len)break;Bar(b,bi,ro+cd*a,upE,cd,n,floor);}
                if(j0==0||!e[i0+(j0-1)*nu])Strip(b,bi,ro,acrossE,new V3(0,BarRail,0),n,BarInset*.5f,floor);
            }
        }

        /// <summary>A fire escape flight's own soffit: the stair's underside, a sloped iron plate under its treads from stringer to
        /// stringer (seen through the grille), wound to face down.</summary>
        private static void FlightSoffit(Builder b,MapRamp r)
        {
            var F=LineOf(r);if(F.Run<.1f)return;
            var o=F.Low+F.Across*(-F.Width*.5f);o.y=F.Line(0)-Stringer;var u=F.Across*F.Width;var v=F.Dir*F.Run+new V3(0,F.Slope*F.Run,0);
            if(Cross(u,v).y>0){o=o+u;u=u*-1;}
            int bi=r.blocks!=null&&r.blocks.Length>0&&b.TreadIndex.TryGetValue(r.blocks[0],out int t0)?t0:-1;
            b.Quad(o,u,v,NativeMeridianSurfaces.IronBlack,0,bi,F.Bottom);
        }
    }
}
