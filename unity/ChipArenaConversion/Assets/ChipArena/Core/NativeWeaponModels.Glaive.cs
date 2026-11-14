using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Glaive (owner 2026-11-09: a thrown ring blade; original geometry; docs/GLAIVE.md). Redesigned at the owner's word the same day ("make
    // the glaive a better design"): the tri-crescent. A heavy stepped ring of blade round the fist, three curved crescent fangs sweeping out
    // past its rim (each raked a little out of its plane, so the ring reads as a pinwheel even edge on), every cutting edge a lit line in the
    // pilot's accent, a lit inner rim and inlaid arcs on both faces, held by a guarded grip bar across its near side with a glowing gem at
    // its middle. In the weapon frame the fist is at the origin, the ring's middle GlaiveCentre out along +z and its plane the y-z plane (so
    // the edges lead across the swing). Rigid: it never stretches.
    public static partial class NativeWeaponModels
    {
        /// <summary>The ring's middle out from the fist (weapon frame, m), its body's outer radius and its inner rim's, and how far the
        /// crescents' tips reach from its middle.</summary>
        public const float GlaiveCentre=.24f,GlaiveOuter=.31f,GlaiveInner=.2f,GlaiveFang=.45f;
        static partial void Glaives(Dictionary<string,Design> d)
        {
            d[NativeGlaive.Spec]=new Design{Spec=NativeGlaive.Spec,Build=Glaive,BladeTip=V(0,0,GlaiveCentre+GlaiveOuter),BladeBase=V(0,0,GlaiveCentre-GlaiveInner),Support=V(0,0,-.08f)};
        }
        private static void Glaive(WeaponSculpt k)
        {
            var steel=Paint(0xd3dbe6,.25f);var plate=Paint(0x8e98a8,.16f);var dark=Paint(0x23272f,.05f);var wrap=Paint(0x2e2421,.02f);var band=Paint(0xb0b9c6,.2f);
            var edge=WeaponPaint.Accent(2.4f,.6f);var core=WeaponPaint.Accent(1.6f,.35f);var gem=WeaponPaint.Accent(3f,.5f);
            k.Into(RightHand,WeaponStretch.Rigid);int sides=k.Sides(40);float i=GlaiveInner,o=GlaiveOuter;
            // The ring: its plane the y-z plane, round its middle out along +z (in the pushed frame the ring lies in x-y, its thickness z).
            k.Push(Ry((float)Math.PI/2)*T(0,0,GlaiveCentre));
            // The body's cross section, stepped: a thick dark hub at the inner rim, a raised steel band, then the blade thinning to a bevel.
            k.Lathe(seg=>seg==3||seg==4?edge:seg==2||seg==5?steel:seg==1||seg==6?plate:dark,new[]{i,-.024f, i+.032f,-.024f, i+.038f,-.014f, o-.034f,-.009f, o,0f, o-.034f,.009f, i+.038f,.014f, i+.032f,.024f, i,.024f, i,-.024f},sides,1,0,false);
            // The lit inner rim, and an inlaid arc in each third of both faces (the dark groove and the light in it).
            k.Torus(core,Vector3.Zero,i+.004f,.006f,sides,k.Sides(6));
            for(int f=-1;f<=1;f+=2)for(int h=0;h<3;h++)
            {
                float a0=h*2.0944f+.35f,a1=a0+1.45f,r=(i+o)*.5f+.012f;
                if(k.Mid)k.Torus(dark,V(0,0,f*.0145f),r,.0042f,k.Sides(12),k.Sides(4),a0,a1);
                if(k.Fine)k.Torus(core,V(0,0,f*.0165f),r,.0018f,k.Sides(12),k.Sides(3),a0+.05f,a1-.05f);
            }
            // Three crescent fangs off the rim, sweeping with the spin, each pitched out of the plane the same way (a turbine's blades).
            for(int h=0;h<3;h++)
            {
                float a=h*2.0944f+.15f,sweep=1.35f;var mid=a+sweep*.5f;var axis=V((float)Math.Cos(mid),(float)Math.Sin(mid),0);
                k.Push(Matrix4x4.CreateFromAxisAngle(axis,.2f));
                var outline=new List<Vector3>();var cut=new List<Vector3>();int n=k.Sides(12);
                for(int s=0;s<=n;s++)
                {
                    float t=s/(float)n,th=a+sweep*t,r=o-.012f+(GlaiveFang-o+.012f)*(float)Math.Pow(t,1.15f);var p=V(r*(float)Math.Cos(th),r*(float)Math.Sin(th),0);
                    outline.Add(p);if(t>.12f)cut.Add(V(p.X*1.0f,p.Y*1.0f,0));
                }
                for(int s=1;s<=n;s++)
                {
                    float u=s/(float)n,th=a+sweep-sweep*.8f*u,r=(o-.02f)+(GlaiveFang-(o-.02f))*(float)Math.Pow(1-u,2.6f);
                    outline.Add(V(r*(float)Math.Cos(th),r*(float)Math.Sin(th),0));
                }
                k.Slab(_=>steel,plate,plate,outline.ToArray(),-.008f,.008f,.003f);
                // The fang's cutting edge lit along its outer curve, and a dark spine line inside it.
                k.Tube(edge,cut,.0035f,k.Sides(5),Vector3.UnitZ,1,true,false,.55f);
                if(k.Mid)
                {
                    var spine=new List<Vector3>();for(int s=0;s<=6;s++){float t=s/6f,th=a+.12f+sweep*.62f*t,r=o+.012f+(GlaiveFang-o-.07f)*t*t;spine.Add(V(r*(float)Math.Cos(th),r*(float)Math.Sin(th),0));}
                    k.Tube(dark,spine,.0024f,k.Sides(4),Vector3.UnitZ,1,true,false,.9f);
                }
                k.Pop();
            }
            // Small notched teeth between the fangs.
            if(k.Mid)for(int h=0;h<3;h++){float a=h*2.0944f+1.75f;var r=V((float)Math.Cos(a),(float)Math.Sin(a),0);k.Box(steel,r*(o+.012f),V(.03f,.016f,.009f),k.Bevel(.002f),0,0,a);k.Box(edge,r*(o+.026f),V(.016f,.004f,.004f),0,0,0,a);}
            k.Pop();
            // The grip across the ring's near side where the fist closes: a wrapped bar, steel collars and a lit emitter stud past each.
            k.Push(Rx((float)Math.PI/2)*T(0,0,GlaiveCentre-GlaiveInner+.012f));
            k.Cylinder(wrap,.018f,-.07f,.07f,k.Sides(10),k.Bevel(.003f));
            for(int s=-1;s<=1;s+=2){k.Cylinder(band,.023f,s*.074f-.009f,s*.074f+.009f,k.Sides(10),k.Bevel(.002f));k.Sphere(gem,V(0,0,s*.093f),V(.014f,.014f,.014f),k.Sides(6),k.Sides(8));}
            if(k.Mid)for(int w=-2;w<=2;w++)k.Cylinder(dark,.0188f,w*.026f-.003f,w*.026f+.003f,k.Sides(10),0);
            k.Pop();
        }
    }
}
