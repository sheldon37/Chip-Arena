using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Fangs (owner 2026-11-10: DEV twin daggers, "very high quality ... really great designs"; original geometry). One fang is
    // built for the right hand and the Design mirrors it onto the left (Dual, MirrorLeft). In the sword family's frame: the fist at
    // the origin, the blade out of it along +z (a forward grip), its flats +-y, the honed edge on +x. The blade is a serpent's
    // fang: thick at the root, bowing back over its spine and hooking forward again to a needle point, blackened steel with a
    // bright honed edge and a venom channel sunk down both flats that is lit in the pilot's accent. It stands in a serpent's
    // gape: a scaled brow block with a lit slit eye on each flat, the upper jaw a long horn curling out past the spine, the lower
    // jaw a short hook under the edge (a finger guard), small teeth inside both. A cord-wrapped grip between two collars, a lit
    // ring, and a fang-tip pommel curling the blade's way, so the whole dagger reads as one crescent. The hilt is Rigid; the blade
    // stretches from the gape (StretchBase) to the point. Presentation only: nothing here changes reach or hits.
    public static partial class NativeWeaponModels
    {
        /// <summary>Along the grip axis (m): the pommel's point, the grip's two ends, where the blade leaves the gape (the blade base
        /// and the stretch base), and the point.</summary>
        public const float FangButtZ=-.13f,FangGripZ0=-.078f,FangGripZ1=.05f,FangBaseZ=.086f,FangTipZ=.42f;
        /// <summary>The blade's width and thickness at its root, how far its middle bows back toward -x, and how far the point
        /// comes forward of the grip axis toward +x (m).</summary>
        public const float FangWidth=.056f,FangThick=.021f,FangBow=.034f,FangHook=.03f;
        /// <summary>The sockets (weapon frame): the point, and the blade's middle at the gape.</summary>
        public static readonly Vector3 FangBladeTip=V(FangHook,0,FangTipZ),FangBladeBase=V(0,0,FangBaseZ);
        /// <summary>The Fangs' Design: one fang in each hand, the left mirrored (not registered here: the class's wiring adds it).</summary>
        public static Design FangsDesign(string spec)=>new Design{Spec=spec,Build=BuildFang,Dual=true,MirrorLeft=true,StretchBase=FangBaseZ,StretchTip=FangTipZ,BladeTip=FangBladeTip,BladeBase=FangBladeBase};

        /// <summary>The blade's middle line off the grip axis at u (0 the root, 1 the point).</summary>
        public static float FangLine(float u)=>-FangBow*(float)Math.Sin(Math.PI*Math.Pow(u,.9))+FangHook*(float)Math.Pow(u,2.5);
        /// <summary>The blade's width at u: a belly a third of the way out, then a long taper to the needle.</summary>
        public static float FangWide(float u)=>u>=1?0:FangWidth*(float)Math.Pow(1-u,.8)*(1+.26f*(float)Math.Sin(Math.PI*u));
        // One section of the fang (14 points, counter-clockwise from the edge): the edge at xe, the spine at xs, thickness t, the venom
        // channel `open` (0 closed .. 1). Bands: 0/13 the honed edge, 1/12 and 5/8 the flats, 2/4/9/11 the channel's walls, 3/10 its
        // lit floor, 6/7 the spine.
        private static Vector3[] FangRing(float z,float xs,float xe,float t,float open)
        {
            float w=xe-xs;if(w<=1e-4f)return Point(14,V(xe,0,z));
            float ht=t/2,gx=xs+w*.42f,g=w*.1f*open+.0002f,dep=t*.3f*open;
            var o=new Vector3[14];
            o[0]=V(xe,0,z);o[1]=V(xe-w*.17f,ht*.6f,z);o[2]=V(gx+g,ht,z);o[3]=V(gx+g*.5f,ht-dep,z);o[4]=V(gx-g*.5f,ht-dep,z);o[5]=V(gx-g,ht,z);o[6]=V(xs+w*.09f,ht*.8f,z);o[7]=V(xs,0,z);
            for(int i=1;i<7;i++)o[14-i]=V(o[i].X,-o[i].Y,z);
            return o;
        }
        // The hood's section (6 points, counter-clockwise): a flat lens across the blade's plane, half as wide as hw, t thick.
        private static Vector3[] HoodRing(float z,float cx,float hw,float t)
            =>new[]{V(cx+hw,0,z),V(cx+hw*.5f,t/2,z),V(cx-hw*.5f,t/2,z),V(cx-hw,0,z),V(cx-hw*.5f,-t/2,z),V(cx+hw*.5f,-t/2,z)};
        // The hood's rings: z, middle, half width, thickness.
        private static readonly float[] FangHood={.048f,0,.0215f,.036f, .056f,0,.027f,.038f, .066f,-.002f,.042f,.036f, .078f,-.004f,.056f,.031f, .088f,-.005f,.062f,.026f, .094f,-.005f,.057f,.018f};
        // The hood's flat face height at z (its thickness there, halved).
        private static float HoodFace(float z)
        {
            float[] h=FangHood;for(int i=0;i+7<h.Length;i+=4)if(z<=h[i+4])return (h[i+3]+(h[i+7]-h[i+3])*Math.Max(0,(z-h[i])/(h[i+4]-h[i])))/2;
            return h[h.Length-1]/2;
        }
        /// <summary>One fang, in the right hand's frame (two sections: the Rigid hilt, the Blade that stretches with reach).</summary>
        public static void BuildFang(WeaponSculpt k)
        {
            var black=Paint(0x1d2227,.05f);var steel=Paint(0x56646c,.1f);var dark=Paint(0x2b343a,.06f);var edge=Paint(Edge,.3f);var pit=Paint(0x0e1114,.02f);
            var brass=Paint(0xb08a4c,.12f);var wrap=Paint(0x2a303a,.02f);var cord=Paint(0x1c2027,.01f);
            var venom=WeaponPaint.Accent(2f,.45f);var mark=WeaponPaint.Accent(1.7f,.4f);var gem=WeaponPaint.Accent(2.6f,.5f);
            k.Into(RightHand,WeaponStretch.Rigid);
            // The fang-tip pommel: a collar, a lit ring, and a small fang curling the way the blade's point does.
            k.Lathe(i=>i==1?brass:steel,new[]{.0125f,FangGripZ0-.02f,.0235f,FangGripZ0-.014f,.0235f,FangGripZ0-.006f,.0195f,FangGripZ0},k.Sides(10),0,Pi/10,true,1,.86f);
            if(k.Mid)k.Torus(mark,V(0,0,FangGripZ0-.0025f),.0215f,.0026f,k.Sides(12),4,0,2*Pi,.86f);
            var claw=new List<Vector3>{V(0,0,FangGripZ0-.016f),V(.001f,0,FangGripZ0-.028f),V(.006f,0,FangGripZ0-.04f),V(.016f,0,FangButtZ+.004f),V(.031f,0,FangButtZ)};
            if(!k.Mid)claw.RemoveAt(2);
            k.Tube(i=>i==0?steel:dark,claw,i=>i==claw.Count-1?.0012f:.0125f-.0095f*i/(claw.Count-1),k.Sides(6),Vector3.UnitY,1,true,false,.8f);
            // The grip: cord over leather, a little swell, a touch flat across the blade's plane.
            WrappedGrip(k,FangGripZ0,FangGripZ1,.019f,.1f,wrap,cord,steel,7,.84f);
            // The hood: spread like a cobra's round the blade's root, its two edges steel, its faces dark.
            var hood=new List<Vector3[]>();for(int i=0;i<FangHood.Length;i+=4)if(k.Mid||i!=4)hood.Add(HoodRing(FangHood[i],FangHood[i+1],FangHood[i+2],FangHood[i+3]));
            k.Loft(hood,(i,s)=>s==1||s==4?dark:steel,0,true,true,steel,false,false,steel);
            // A brass collar where the blade stands out of it.
            k.Loft(new[]{HoodRing(.092f,-.001f,.034f,.03f),HoodRing(.099f,-.001f,.034f,.03f),HoodRing(.102f,-.001f,.031f,.025f)},(i,s)=>i==1?brass.Scaled(.8f):brass,0,false,true);
            // Its two tips sweep on past the root: the longer round the spine, the shorter under the edge (a finger guard).
            var upper=new List<Vector3>{V(-.056f,0,.084f),V(-.07f,0,.098f),V(-.075f,0,.118f),V(-.071f,0,.136f),V(-.061f,0,.152f)};
            var lower=new List<Vector3>{V(.05f,0,.084f),V(.063f,0,.095f),V(.067f,0,.11f),V(.062f,0,.126f)};
            if(!k.Mid){upper.RemoveAt(3);lower.RemoveAt(2);}
            k.Tube(i=>steel,upper,i=>i==upper.Count-1?.0012f:.0105f-.007f*i/(upper.Count-1),k.Sides(6),Vector3.UnitY,1,true,false,.62f);
            k.Tube(i=>steel,lower,i=>i==lower.Count-1?.0012f:.0105f-.007f*i/(lower.Count-1),k.Sides(6),Vector3.UnitY,1,true,false,.62f);
            // The hood's mark on both faces: a lit chevron opening toward the blade, a lit stone in a brass seat at its foot.
            for(int s=-1;s<=1;s+=2)
            {
                var n=V(0,s,0);Vector3 On(float x,float z,float lift)=>V(x,s*(HoodFace(z)+lift),z);
                if(k.Mid)for(int arm=-1;arm<=1;arm+=2)
                {
                    // Each arm in two lengths, broken where the hood's face turns.
                    float x0=arm*.008f-.003f,x1=arm*.026f-.005f,z0=.0735f,z1=.0875f,zm=.078f,xm=x0+(x1-x0)*(zm-z0)/(z1-z0),w=.0028f,lift=.0009f;
                    QuadTo(k,mark,On(x0,z0-w,lift),On(x0,z0+w,lift),On(xm,zm+w,lift),On(xm,zm-w,lift),n);
                    QuadTo(k,mark,On(xm,zm-w,lift),On(xm,zm+w,lift),On(x1,z1+w*.3f,lift),On(x1,z1-w,lift),n);
                }
                k.Push(Rx(-s*Pi/2)*T(-.003f,s*(HoodFace(.0665f)-.0008f),.0665f));
                if(k.Mid)k.Cylinder(brass,.0092f,0,.0024f,6,0);
                k.Gem(gem,V(0,0,.0024f),.0066f,.005f,6,.02f);k.Pop();
            }
            // The blade: the fang, root to needle.
            k.Into(RightHand,WeaponStretch.Blade);
            int rows=k.Fine?32:k.Mid?16:8;var rings=new List<Vector3[]>();
            for(int i=0;i<=rows;i++)
            {
                // Rings gather toward the point, where the hook turns hardest.
                float u=1-(float)Math.Pow(1-(float)i/rows,1.25f),z=FangBaseZ+(FangTipZ-FangBaseZ)*u,cx=FangLine(u),w=FangWide(u),t=.0035f+(FangThick-.0035f)*(float)Math.Pow(1-u,.7f);
                float open=Math.Min(1,Math.Max(0,(u-.05f)/.07f))*Math.Min(1,Math.Max(0,(.88f-u)/.2f));
                rings.Add(FangRing(z,cx-w/2,cx+w/2,t,open));
            }
            k.Loft(rings,(i,s)=>s==0||s==13?edge:s==3||s==10?venom:s==2||s==4||s==9||s==11?pit:s==6||s==7?steel:black,0,false,false);
        }
    }
}
