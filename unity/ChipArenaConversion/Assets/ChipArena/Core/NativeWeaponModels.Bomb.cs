using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Bombadier's launcher, the Kickdrum (original geometry; docs/BOMBADIER.md): a stubby six-chamber drum launcher, part workshop project. Bore on y = 0: a fat
    // receiver and a pistol grip where the long guns keep theirs, the free hand on the receiver's front (Support), a big revolving drum with the bombs' red noses showing,
    // hazard stripes round it, a short flared barrel with brass bands, a pressure gauge, a coil of fuse on the side, and a wrapped wooden stock.
    public static partial class NativeWeaponModels
    {
        public const float KickdrumMuzzle=.74f;
        static partial void Bombs(Dictionary<string,Design> d)
        {
            d[NativeBombs.Spec]=new Design{Spec=NativeBombs.Spec,Build=Kickdrum,Muzzle=V(0,0,KickdrumMuzzle),Support=V(.03f,-.05f,.1f)};
        }
        private static void Kickdrum(WeaponSculpt k)
        {
            var metal=Paint(0x33363a,.05f);var dark=Paint(0x1c1e20,.03f);var brassy=Paint(Brass,.18f);var steel=Paint(Steel,.1f);var black=Paint(Black);var rubber=Paint(Rubber);var wood=Paint(0x5e3a22,.01f);var leather=Paint(0x2b1c13,.01f);
            var hazard=Paint(0xe8b90f,.02f);var red=Paint(0xc9221c,.04f);var drum=Paint(0x3c4044,.06f);var glass=Paint(0xdfe6df,.12f);var fuse=Paint(0x241d17,.01f);
            // The receiver: a thick chamfered box with a top rail, rivets and accent lines.
            var rc=new List<Vector3[]>{Rect(.12f,.15f,.018f,-.14f,0,.012f),Rect(.13f,.162f,.022f,-.13f,0,.012f),Rect(.13f,.162f,.022f,.15f,0,.012f),Rect(.12f,.15f,.018f,.165f,0,.012f)};
            k.Loft(rc,(i,j)=>metal,0);
            k.Box(dark,V(0,.102f,.02f),V(.03f,.012f,.13f),.004f);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(AccentStrip,V(s*.0662f,-.02f,.01f),V(.0016f,.007f,.12f),0);
            if(k.Fine)for(int s=-1;s<=1;s+=2)for(int i=0;i<4;i++){k.Push(Ry(s*Pi/2)*T(s*.065f,.05f,-.1f+i*.08f));k.Cylinder(steel,.006f,0,.003f,6,0);k.Pop();}
            // The grip and its guard.
            k.Push(Rx(.2f)*T(0,-.135f,-.04f));k.Box(rubber,V(0,0,0),V(.028f,.078f,.036f),.009f);if(k.Mid)for(int i=0;i<4;i++)k.Box(dark,V(0,-.05f+i*.033f,.0372f),V(.024f,.004f,.0015f),0);k.Pop();
            k.Tube(_=>steel,new List<Vector3>{V(0,-.075f,.05f),V(0,-.115f,.052f),V(0,-.125f,.018f),V(0,-.08f,-.018f)},_=>.006f,k.Sides(6),Vector3.UnitX,1,true);
            // The stock: wood, a leather wrap and a butt pad.
            k.Box(wood,V(0,-.012f,-.22f),V(.03f,.05f,.09f),.012f);k.Box(rubber,V(0,-.012f,-.305f),V(.033f,.056f,.012f),.006f);
            if(k.Mid){k.Box(leather,V(0,-.012f,-.2f),V(.0325f,.0525f,.028f),.004f);k.Box(leather,V(0,-.012f,-.25f),V(.0325f,.0525f,.012f),.004f);}
            // The drum (axis z), with hazard stripes round it and six chambers showing their bombs.
            const float DrumFrom=.17f,DrumTo=.42f,DrumR=.128f,DrumY=-.022f;
            k.Push(T(0,DrumY,0));
            k.Lathe(i=>i==1?drum:dark,new[]{.09f,DrumFrom,DrumR-.012f,DrumFrom+.006f,DrumR,DrumFrom+.022f,DrumR,DrumTo-.022f,DrumR-.012f,DrumTo-.006f,.09f,DrumTo},k.Sides(20),1,0,true);
            if(k.Mid)
            {
                int stripes=k.Fine?18:12;float seg=DrumR*(float)Math.PI/stripes;
                for(int j=0;j<stripes;j++){double a=j*Math.PI*2/stripes;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);var u=V(0,0,.026f);k.Decal(j%2==0?hazard:black,n*(DrumR+.0007f)+V(0,0,(DrumFrom+DrumTo)*.5f),u,Vector3.Cross(n,u)/.026f*seg);}
                for(int s=0;s<2;s++)k.Torus(brassy,V(0,0,s==0?DrumFrom+.03f:DrumTo-.03f),DrumR+.001f,.004f,k.Sides(20),4);
            }
            for(int j=0;j<6;j++)
            {
                double a=j*Math.PI/3+.26;float cx=.078f*(float)Math.Cos(a),cy=.078f*(float)Math.Sin(a);
                k.Cylinder(dark,.031f,DrumTo-.01f,DrumTo+.004f,k.Sides(10),0,cx,cy);k.Push(T(cx,cy,0));k.Lathe(red,new[]{.024f,DrumTo,.024f,DrumTo+.016f,.012f,DrumTo+.03f,.001f,DrumTo+.034f},k.Sides(10),1,0,true);k.Pop();
                if(k.Mid)k.Torus(brassy,V(cx,cy,DrumTo+.004f),.0255f,.0028f,k.Sides(10),3);
            }
            k.Cylinder(steel,.022f,DrumTo-.01f,DrumTo+.018f,k.Sides(8),.003f,0,0);
            k.Pop();
            // The barrel: short and fat, brass bands, a flared muzzle with a hazard ring.
            k.Cylinder(metal,.05f,DrumTo-.01f,.66f,k.Sides(14),0,0,0);
            k.Lathe(i=>i==1?steel:metal,new[]{.05f,.64f,.056f,.655f,.07f,.71f,.078f,KickdrumMuzzle-.004f,.072f,KickdrumMuzzle,.054f,KickdrumMuzzle-.005f},k.Sides(14),1,0,true);
            Disc(k,black,0,0,KickdrumMuzzle-.004f,.054f,k.Sides(14));
            k.Torus(brassy,V(0,0,.46f),.0515f,.005f,k.Sides(14),4);k.Torus(brassy,V(0,0,.6f),.0515f,.005f,k.Sides(14),4);
            if(k.Mid){int st=k.Fine?14:10;float seg=.052f*(float)Math.PI/st;for(int j=0;j<st;j++){double a=j*Math.PI*2/st;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);var u=V(0,0,.018f);k.Decal(j%2==0?hazard:black,n*.0507f+V(0,0,.53f),u,Vector3.Cross(n,u)/.018f*seg);}}
            // A sight bead on a post.
            k.Box(dark,V(0,.066f,.62f),V(.006f,.018f,.012f),.002f);
            // The pressure gauge on top, its needle in the red, and a red valve knob.
            k.Cylinder(brassy,.03f,-.09f,-.05f,k.Sides(12),.004f,0,.128f);
            if(k.Mid){Disc(k,glass,0,.128f,-.0498f,.024f,k.Sides(12));k.Box(red,V(.008f,.134f,-.0492f),V(.012f,.0016f,.0006f),0);}
            k.Cylinder(red,.012f,-.03f,-.012f,k.Sides(8),.003f,0,.122f);k.Box(dark,V(0,.11f,-.021f),V(.006f,.012f,.006f),0);
            // A coil of fuse on the left of the receiver and its loose end, trailing back.
            k.Torus(fuse,V(.076f,.03f,-.05f),.04f,.0062f,k.Sides(14),4);if(k.Mid)k.Torus(fuse,V(.0812f,.03f,-.05f),.03f,.0062f,k.Sides(12),4);
            k.Tube(_=>fuse,new List<Vector3>{V(.078f,.066f,-.06f),V(.086f,.09f,-.11f),V(.09f,.076f,-.17f),V(.094f,.1f,-.21f)},_=>.005f,k.Sides(5),Vector3.UnitY,1,true);
            k.Box(AccentStrip,V(.094f,.104f,-.214f),V(.008f,.008f,.008f),.002f);
        }
    }
}
