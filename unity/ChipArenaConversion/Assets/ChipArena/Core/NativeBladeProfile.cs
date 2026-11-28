using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A cross-section of a drawn melee blade at one z of its weapon frame (the source frame: +x the cutting edge's side, +y a
    /// flat, before the reach stretch): the cutting edge and its half-thickness there (0: a sharp V), where the edge band (the
    /// bevel, or the Prototype Series' bright edge strip) meets the flat and the half-thickness there, and the blade's back.
    /// </summary>
    public struct BladeSection
    {
        public float Edge,EdgeHalf,Inner,InnerHalf,Back;
        public static BladeSection Lerp(in BladeSection a,in BladeSection b,float t)=>new BladeSection{Edge=a.Edge+(b.Edge-a.Edge)*t,EdgeHalf=a.EdgeHalf+(b.EdgeHalf-a.EdgeHalf)*t,
            Inner=a.Inner+(b.Inner-a.Inner)*t,InnerHalf=a.InnerHalf+(b.InnerHalf-a.InnerHalf)*t,Back=a.Back+(b.Back-a.Back)*t};
    }

    /// <summary>
    /// The drawn blade's shape along its length, for the sword family's two skins, so effects can sit on the steel instead of
    /// beside it. Standard: the same rings the models are lofted from (NativeWeaponModels.Melee: the sword's and greatsword's
    /// bevelled, fullered sections, the daggers' curved wedge with its full-detail smoothing), under the same blade scale; the
    /// loft is straight between rings, so so is the profile. Prototype Series: the exported 'weapon-' bank boxes (a darker
    /// core box with a bright edge strip either side, stepped tip boxes; the dagger's tip box is turned 20 degrees toward its
    /// edge). Presentation only.
    /// </summary>
    public static class NativeBladeProfile
    {
        private sealed class Table
        {
            public float[] Z,Edge,EdgeHalf,Inner,InnerHalf,Back;
            /// <summary>The blade's bloodable run: from the guard, lugs or ricasso to the point.</summary>
            public float From,Tip;
            public BladeSection At(int i)=>new BladeSection{Edge=Edge[i],EdgeHalf=EdgeHalf[i],Inner=Inner[i],InnerHalf=InnerHalf[i],Back=Back[i]};
        }
        private static Table swordStd,daggersStd,greatswordStd,swordProto,daggersProto,greatswordProto;
        public static bool Bladed(string spec)=>spec=="sword"||spec=="daggers"||spec=="greatsword";
        private static Table Of(string spec,bool standard)
        {
            switch(spec)
            {
                case "sword":return standard?swordStd??=Rings(NativeWeaponModels.SwordBladeZ,NativeWeaponModels.SwordBladeW,NativeWeaponModels.SwordBladeF,NativeWeaponModels.SwordBladeT,NativeWeaponModels.SwordBladeE,NativeWeaponModels.SwordBladeScale,.31f)
                    :swordProto??=Boxes(new[]{.26f,1.12f,1.12f,1.20f,1.20f,1.27f},new[]{.05f,.05f,.033f,.033f,.0165f,.0165f},new[]{.03f,.03f,.013f,.013f,0,0},new[]{.015f,.015f,.0175f,.0175f,.0175f,.0175f},.30f,1.27f);
                case "greatsword":return standard?greatswordStd??=Rings(NativeWeaponModels.GreatswordBladeZ,NativeWeaponModels.GreatswordBladeW,NativeWeaponModels.GreatswordBladeF,NativeWeaponModels.GreatswordBladeT,_=>NativeWeaponModels.GreatswordBladeE,NativeWeaponModels.GreatswordBladeScale,.49f)
                    :greatswordProto??=Boxes(new[]{.26f,1.53f,1.53f,1.61f,1.61f,1.68f},new[]{.09f,.09f,.0594f,.0594f,.0297f,.0297f},new[]{.054f,.054f,.0394f,.0394f,.0097f,.0097f},new[]{.015f,.015f,.0175f,.0175f,.0175f,.0175f},.40f,1.68f);
                // The Prototype dagger's tip box (z .662-.828) runs its edge side from x .018 at z .662 out to .0695 at z .803.
                case "daggers":return standard?daggersStd??=Dagger()
                    :daggersProto??=Boxes(new[]{.26f,.68f,.68f,.8031f,.8031f,.8278f},new[]{.045f,.045f,.0246f,.0695f,.0695f,.0019f},new[]{.027f,.027f,.0046f,.0495f,.0495f,-.0181f},new[]{.015f,.015f,.015f,.015f,.015f,.015f},.30f,.80f);
                default:return null;
            }
        }
        // A Standard blade lofted from BladeRing sections (NativeWeaponModels.BladeRing: the edge at +-w/2, the bevel up to
        // max(w/2-e, fuller/2+.0008) at half the thickness), scaled like the model.
        private static Table Rings(float[] z,float[] w,float[] f,Func<int,float> thickness,Func<int,float> bevel,Vector3 scale,float from)
        {
            int n=z.Length;var t=new Table{Z=new float[n],Edge=new float[n],EdgeHalf=new float[n],Inner=new float[n],InnerHalf=new float[n],Back=new float[n],From=from,Tip=z[n-1]};
            for(int i=0;i<n;i++)
            {
                t.Z[i]=z[i];if(w[i]<=1e-4f)continue;
                float hw=w[i]/2,fx=Math.Max(f[i]/2,.0004f),ix=Math.Max(hw-bevel(i),fx+.0008f);
                t.Edge[i]=hw*scale.X;t.Inner[i]=ix*scale.X;t.InnerHalf[i]=thickness(i)/2*scale.Y;t.Back[i]=-t.Edge[i];
            }
            return t;
        }
        // The Standard daggers: the curved wedge (NativeWeaponModels.Wedge: the edge at xe, its bevel up to xe-(xe-xs)*.42 at .3
        // of the thickness, the spine at xs) with the full-detail midpoints between the control rings.
        private static Table Dagger()
        {
            float[] Z=NativeWeaponModels.DaggerBladeZ,XS=NativeWeaponModels.DaggerBladeXs,XE=NativeWeaponModels.DaggerBladeXe;var scale=NativeWeaponModels.DaggerBladeScale;
            int n=Z.Length+Z.Length-2;var t=new Table{Z=new float[n],Edge=new float[n],EdgeHalf=new float[n],Inner=new float[n],InnerHalf=new float[n],Back=new float[n],From=.22f,Tip=Z[Z.Length-1]};
            int k=0;
            void Add(float z,float xs,float xe,float th)
            {
                t.Z[k]=z;float g=xe-(xe-xs)*.42f;bool point=xe-xs<=1e-4f;
                t.Edge[k]=(point?xs:xe)*scale.X;t.Inner[k]=(point?xs:g)*scale.X;t.InnerHalf[k]=point?0:th*.3f*scale.Y;t.Back[k]=xs*scale.X;k++;
            }
            for(int i=0;i<Z.Length;i++)
            {
                Add(Z[i],XS[i],XE[i],NativeWeaponModels.DaggerBladeT(i));
                if(i+1<Z.Length-1){NativeWeaponModels.DaggerMid(i,out float mz,out float ms,out float me,out float mt);Add(mz,ms,me,mt);}
            }
            return t;
        }
        // A Prototype blade: its box spans (a repeated z is a step), the edge strip flat on both faces.
        private static Table Boxes(float[] z,float[] edge,float[] inner,float[] half,float from,float tip)
        {
            int n=z.Length;var t=new Table{Z=z,Edge=edge,EdgeHalf=half,Inner=inner,InnerHalf=half,Back=new float[n],From=from,Tip=tip};
            for(int i=0;i<n;i++)t.Back[i]=-edge[i];return t;
        }
        /// <summary>The blade's run in its weapon frame: where blood may start (past the guard) and its point.</summary>
        public static bool Range(string spec,bool standard,out float from,out float tip)
        {
            var t=Of(spec,standard);from=t?.From??0;tip=t?.Tip??0;return t!=null;
        }
        /// <summary>The span (knot index i to i+1) holding z; at a step, the span above it.</summary>
        private static int SpanOf(Table t,float z){int last=t.Z.Length-2;for(int i=0;i<=last;i++)if(z<t.Z[i+1]||i==last)return i;return last;}
        /// <summary>The section at z (clamped to the blade). False for a spec without a blade.</summary>
        public static bool At(string spec,bool standard,float z,out BladeSection s)
        {
            s=default;var t=Of(spec,standard);if(t==null)return false;z=Math.Max(t.Z[0],Math.Min(t.Z[t.Z.Length-1],z));
            int i=SpanOf(t,z);float d=t.Z[i+1]-t.Z[i];s=BladeSection.Lerp(t.At(i),t.At(i+1),d>1e-6f?(z-t.Z[i])/d:0);return true;
        }
        /// <summary>
        /// Both ends of a straight piece from z0 to z1 lying in one span (the one holding its middle): the blade's edge is straight
        /// there, so a box between these ends hugs it. Use Steps to keep a piece from crossing a Prototype step.
        /// </summary>
        public static bool Span(string spec,bool standard,float z0,float z1,out BladeSection a,out BladeSection b)
        {
            a=b=default;var t=Of(spec,standard);if(t==null)return false;int i=SpanOf(t,Math.Max(t.Z[0],Math.Min(t.Z[t.Z.Length-1],(z0+z1)*.5f)));
            // An end inside the middle's span reads that span (so a piece ending on a step keeps its own side of it); an end
            // past it reads the blade there (the chord across a ring, which lies inside a tapering edge).
            BladeSection End(float z)
            {
                float d=t.Z[i+1]-t.Z[i];if(z>=t.Z[i]-1e-6f&&z<=t.Z[i+1]+1e-6f)return BladeSection.Lerp(t.At(i),t.At(i+1),d>1e-6f?(z-t.Z[i])/d:0);
                At(spec,standard,z,out var s);return s;
            }
            a=End(z0);b=End(z1);return true;
        }
        /// <summary>The z of step k (a knot repeated for a discontinuity: a Prototype tip box) inside (from, to); false past the last.</summary>
        public static bool Step(string spec,bool standard,int k,float from,float to,out float z)
        {
            z=0;var t=Of(spec,standard);if(t==null)return false;int seen=0;
            for(int i=1;i<t.Z.Length;i++)if(t.Z[i]==t.Z[i-1]&&t.Z[i]>from&&t.Z[i]<to){if(seen==k){z=t.Z[i];return true;}seen++;}
            return false;
        }
    }

    /// <summary>
    /// Blood on a melee blade that drew it (owner 2026-10-15: the blood was "rendering off of the weapons like they're
    /// floating"): a thin wet coat on both faces of the edge band, a darker core where it gathers at the edge, a bead along the
    /// edge itself and a few drops clinging to it, all lying on the drawn blade's surface (NativeBladeProfile) of the skin the
    /// pilot shows. `amount` (0..1, Core/NativeHitReact's ledger) carries it further back from the point and thickens it. In the
    /// held weapon's frame (source frame, before the reach stretch, which the runtime applies as it does to the blade):
    /// queued like the chip modules (x mirrored at the Unity boundary). Deterministic and allocation free.
    /// </summary>
    public static class NativeBladeBlood
    {
        public const int MaxPieces=64,MaxSegments=9;
        /// <summary>Segments along the wet run: shorter blades need fewer to follow their taper (five pieces each).</summary>
        public static int Segments(string spec)=>spec=="greatsword"?9:spec=="sword"?8:6;
        /// <summary>The wet coat, the darker core and the edge bead, and the drops (sRGB): blood on steel reads dark.</summary>
        public const int CoatColor=0x780a14,CoreColor=0x4a050d,BeadColor=0x36030a,DropColor=0x5c0711;
        private const float Lift=.0004f;
        private static float Hash(int spec,int k){unchecked{uint h=(uint)(spec*374761393+k*668265263);h=(h^(h>>13))*1274126177u;h^=h>>16;return (h&0xffffff)/16777216f;}}
        /// <summary>
        /// The share of the edge band the blood covers on one face (-1, +1) at u along the wet run (0 its back end, 1 the point):
        /// more toward the point and with more blood, thinning out toward the back, unevenly and differently on each face.
        /// </summary>
        public static float Cover(float amount,float u,int face,int id)
        {
            u=MeleeMath.Clamp(u,0,1);float wave=(float)Math.Sin(u*9.3f+face*1.7f+id*2.1f)*.6f+(float)Math.Sin(u*23.7f+face*.9f+id)*.4f;
            return MeleeMath.Clamp((.26f+.62f*amount)*(.35f+.65f*(float)Math.Pow(u,.6))*(1+.24f*wave),.1f,1);
        }
        /// <summary>
        /// The pieces for one blade into o from `start` (mirror: the Standard left dagger, built mirrored across its blade
        /// plane); returns the new count. None below a trace of blood or for a spec without a blade.
        /// </summary>
        public static int Pieces(string spec,bool standard,float amount,bool mirror,WeaponPiece[] o,int start=0)
        {
            int n=start;amount=MeleeMath.Clamp(NativeBloodRules.Finite(amount,0),0,1);if(o==null||amount<.02f||!NativeBladeProfile.Range(spec,standard,out float from,out float tip))return n;
            int id=spec=="daggers"?2:spec=="greatsword"?3:1;
            // The wet run: from just short of the point back along the edge, further and thicker with more blood.
            // The point itself stays bright steel (a drop hangs there instead): blood stops where the blade is still a few cm wide.
            float end=tip-(standard?(spec=="daggers"?.035f:.05f):.02f),begin=end-(end-from)*(.3f+.62f*amount),run=end-begin;if(run<.02f)return n;
            float coat=.0006f+.0006f*amount,core=coat*1.8f;
            int segments=Segments(spec);
            for(int i=0;i<segments;i++)
            {
                // Each segment, split again at a Prototype step so it never bridges one.
                float z0=begin+run*i/segments,z1=begin+run*(i+1)/segments;
                for(int part=0;part<2;part++)
                {
                    float a0=z0,a1=z1;
                    if(NativeBladeProfile.Step(spec,standard,0,z0,z1,out float cut)){if(part==0)a1=cut;else a0=cut;}else if(part==1)break;
                    if(a1-a0<.004f)continue;
                    if(!NativeBladeProfile.Span(spec,standard,a0,a1,out var sa,out var sb))return n;
                    float u=((a0+a1)*.5f-begin)/run;
                    n=Segment(o,n,a0,a1,sa,sb,Cover(amount,u,1,id),Cover(amount,u,-1,id),coat,core,mirror);
                }
            }
            // Drops clinging to the edge: one to three, more toward the point, and one at the point when it runs to it.
            int drops=1+(int)Math.Round(2*amount);
            for(int k=0;k<drops&&n<o.Length;k++)
            {
                // The drop by the point is rounder: the blade narrows fast there.
                float h=Hash(id,40+k),z=k==0?end+(tip-end)*.3f:end-run*(.12f+.6f*h);if(!NativeBladeProfile.At(spec,standard,z,out var s))break;
                float r=.0028f+.0024f*amount*(.6f+.4f*Hash(id,50+k)),y=(Hash(id,60+k)-.5f)*Math.Max(.002f,s.EdgeHalf);
                n=Put(o,n,WeaponShape.Sphere,DropColor,new Vector3(s.Edge+r*.25f,y,z),new Vector3(r*.8f,r*.7f,r*(k==0?1.1f:1.8f)),Quaternion.Identity,mirror);
            }
            return n;
        }
        // One straight run of the edge band from z0 to z1 (sections a and b): the coat and the core on both faces, lying on the
        // band's surface (the Standard bevel slopes from the edge up to the flat; the Prototype strip is flat), and the bead.
        private static int Segment(WeaponPiece[] o,int n,float z0,float z1,in BladeSection a,in BladeSection b,float top,float bottom,float coat,float core,bool mirror)
        {
            if(n+5>o.Length)return n;
            var m=BladeSection.Lerp(a,b,.5f);float length=z1-z0,yaw=(float)Math.Atan2(b.Edge-a.Edge,length),along=(float)Math.Sqrt(length*length+(b.Edge-a.Edge)*(b.Edge-a.Edge))+.003f;
            float run=m.Edge-m.Inner,rise=m.InnerHalf-m.EdgeHalf,band=(float)Math.Sqrt(run*run+rise*rise);if(!(band>1e-4f)||!(along>1e-4f))return n;
            float dx=-run/band,dy=rise/band;var turn=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);
            for(int face=-1;face<=1;face+=2)
            {
                // The band from the edge (x Edge, y +-EdgeHalf) in to the flat (u), its outward normal on this face (n), and the
                // roll that lays a box's x across the band and its y along n.
                float ex=m.Edge,ey=face*m.EdgeHalf,ux=dx,uy=face*dy,nx=dy,ny=-face*dx;
                float roll=(float)Math.Atan2(-dy,-face*dx);var q=turn*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,roll);
                for(int layer=0;layer<2;layer++)
                {
                    float w=band*(face>0?top:bottom)*(layer==0?1:.45f),t=layer==0?coat:core,lift=Lift+t*.5f+(layer==1?coat*.6f:0);
                    float cx=ex+ux*w*.5f+nx*lift,cy=ey+uy*w*.5f+ny*lift;
                    n=Put(o,n,WeaponShape.Cube,layer==0?NativeBladeBlood.CoatColor:CoreColor,new Vector3(cx,cy,(z0+z1)*.5f),new Vector3(w,t,along),q,mirror);
                }
            }
            // The bead along the edge itself, wrapping it (and the Prototype strip's edge face) over the coats' outer ends.
            float bead=.0024f+core;
            n=Put(o,n,WeaponShape.Cube,BeadColor,new Vector3(m.Edge-bead*.5f+.0008f,0,(z0+z1)*.5f),new Vector3(bead,2*(m.EdgeHalf+Lift+core)+bead*(m.EdgeHalf>0?.3f:1),along),turn,mirror);
            return n;
        }
        private static int Put(WeaponPiece[] o,int n,WeaponShape shape,int color,Vector3 at,Vector3 size,Quaternion q,bool mirror)
        {
            if(n>=o.Length)return n;if(mirror){at.X=-at.X;q=CapturedRig.Mirror(q);}
            o[n]=new WeaponPiece{Shape=shape,Color=color,Position=at,Scale=size,Rotation=q,Orbit=-1};return n+1;
        }
    }
}
