using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Glaive (owner 2026-11-09: a thrown ring blade; original geometry; docs/GLAIVE.md): a ring of blade round the fist, held by a wrapped
    // grip bar across its near side. In the weapon frame the fist is at the origin, the ring's middle GlaiveCentre out along +z and its plane
    // the y-z plane (so the edge leads where a blade's would: across the swing), its cutting edge a thin bevel lit in the pilot's accent, three
    // swept hooks round the rim, two emitter studs either side of the grip. Rigid: it never stretches.
    public static partial class NativeWeaponModels
    {
        /// <summary>The ring's middle out from the fist (weapon frame, m), its outer edge's radius and its inner rim's.</summary>
        public const float GlaiveCentre=.24f,GlaiveOuter=.31f,GlaiveInner=.2f;
        static partial void Glaives(Dictionary<string,Design> d)
        {
            d[NativeGlaive.Spec]=new Design{Spec=NativeGlaive.Spec,Build=Glaive,BladeTip=V(0,0,GlaiveCentre+GlaiveOuter),BladeBase=V(0,0,GlaiveCentre-GlaiveInner),Support=V(0,0,-.08f)};
        }
        private static void Glaive(WeaponSculpt k)
        {
            var steel=Paint(0xc8d2de,.22f);var dark=Paint(0x2a2f38,.05f);var wrap=Paint(0x3a2a24,.02f);var band=Paint(0x8b95a3,.18f);var edge=WeaponPaint.Accent(2.2f,.55f);var core=WeaponPaint.Accent(1.4f,.3f);
            k.Into(RightHand,WeaponStretch.Rigid);int sides=k.Sides(40);
            // The ring: its plane the y-z plane, round its middle out along +z (the lathe's axis is the frame's x).
            k.Push(Ry((float)Math.PI/2)*T(0,0,GlaiveCentre));
            // The blade's cross section: the inner rim thick, thinning to a bevelled edge (the edge's rings lit).
            float i=GlaiveInner,o=GlaiveOuter;
            k.Lathe(seg=>seg==2||seg==3?edge:seg==1||seg==4?steel:dark,new[]{i,-.014f, i+.04f,-.011f, o-.035f,-.005f, o,0f, o-.035f,.005f, i+.04f,.011f, i,.014f, i,-.014f},sides,1,0,false);
            // A darker inlay groove round the middle of the blade and a lit line inside it.
            if(k.Mid){k.Torus(dark,V(0,0,.0115f),(i+o)*.5f-.02f,.0035f,sides,5);k.Torus(dark,V(0,0,-.0115f),(i+o)*.5f-.02f,.0035f,sides,5);}
            if(k.Fine){k.Torus(core,V(0,0,.0135f),(i+o)*.5f-.02f,.0016f,sides,4);k.Torus(core,V(0,0,-.0135f),(i+o)*.5f-.02f,.0016f,sides,4);}
            // Three swept hooks off the rim, raked back against the spin.
            for(int h=0;h<3;h++)
            {
                float a=h*2.0944f+.6f;var r=V((float)Math.Cos(a),(float)Math.Sin(a),0);var t=V(-r.Y,r.X,0);var at=r*(o+.02f)-t*.03f;
                k.Box(steel,at,V(.075f,.022f,.009f),k.Bevel(.003f),0,0,a+.55f);k.Box(edge,at+r*.012f+t*.018f,V(.05f,.006f,.0035f),0,0,0,a+.55f);
            }
            k.Pop();
            // The grip bar across the ring's near side where the fist closes, wrapped, with steel collars, and the emitter studs beside it.
            k.Push(Rx((float)Math.PI/2)*T(0,0,GlaiveCentre-GlaiveInner+.012f));
            k.Cylinder(wrap,.017f,-.065f,.065f,k.Sides(10),k.Bevel(.003f));
            for(int s=-1;s<=1;s+=2){k.Cylinder(band,.021f,s*.07f-.008f,s*.07f+.008f,k.Sides(10),k.Bevel(.002f));k.Sphere(core,V(0,0,s*.09f),V(.013f,.013f,.013f),k.Sides(6),k.Sides(8));}
            if(k.Mid)for(int w=-2;w<=2;w++)k.Cylinder(dark,.0178f,w*.024f-.003f,w*.024f+.003f,k.Sides(10),0);
            k.Pop();
        }
    }
}
