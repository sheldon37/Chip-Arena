using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Top Dog's mallet (owner 2026-11-08: "a cartoon mallet"; original geometry; docs/TOP-DOG.md): a toon mallet, swung two-handed as the
    // greatsword is, in the blades' frame: the grip about the hand at the origin, the handle up +z, the head's barrel across it along y (in the swing's plane), so its
    // striking faces lead where a blade's edge would. A mustard knob, a red and white wrapped grip, a tan handle with a mustard collar, and a big
    // red barrel with mustard bands and domed cream faces. Reach: the grip is Rigid; the handle and head stretch from StretchBase to the tip socket.
    public static partial class NativeWeaponModels
    {
        /// <summary>The head's middle up the handle (weapon frame, m), its barrel radius and half length (the faces are at y = +-HeadHalf).</summary>
        public const float MalletHead=1.12f,MalletRadius=.2f,MalletHalf=.3f;
        static partial void Dogs(Dictionary<string,Design> d)
        {
            d[NativeTopDog.Spec]=new Design{Spec=NativeTopDog.Spec,Build=Mallet,StretchBase=.3f,StretchTip=MalletHead+MalletRadius,BladeTip=V(0,0,MalletHead+MalletRadius),BladeBase=V(0,0,MalletHead-MalletRadius),Support=V(0,0,-.11f)};
        }
        private static void Mallet(WeaponSculpt k)
        {
            var wood=Paint(0xd49a5e,.04f);var woodDark=Paint(0x9c6534,.02f);var red=Paint(0xe2362c,.07f);var redDark=Paint(0xa8221c,.04f);var mustard=Paint(0xf4c430,.1f);var cream=Paint(0xf6ebd0,.06f);var white=Paint(0xf4f1ea,.04f);
            k.Into(RightHand,WeaponStretch.Rigid);
            // The knob and the wrapped grip (both hands hold it, the support hand low on it).
            k.Sphere(mustard,V(0,0,-.205f),V(.043f,.043f,.036f),k.Sides(8),k.Sides(12));
            k.Cylinder(woodDark,.029f,-.19f,-.17f,k.Sides(10),.003f);
            int bands=k.Fine?8:k.Mid?6:4;float g0=-.17f,g1=.24f;for(int i=0;i<bands;i++){float z0=g0+(g1-g0)*i/bands,z1=g0+(g1-g0)*(i+1)/bands;k.Cylinder(i%2==0?red:white,.031f,z0,z1,k.Sides(10),k.Fine?.002f:0);}
            k.Into(RightHand,WeaponStretch.Blade);
            // The handle: tan wood, a little fatter toward the head, and a mustard collar where it meets it.
            k.Lathe(wood,new[]{.027f,g1,.029f,.55f,.032f,MalletHead-MalletRadius+.02f},k.Sides(10),1,0,false);
            k.Cylinder(mustard,.047f,MalletHead-MalletRadius-.03f,MalletHead-MalletRadius+.02f,k.Sides(12),.006f);
            // The head: its barrel along y, in the swing's plane, so a slam or a swing lands on a face (owner 2026-11-09: it smacked down on
            // its side across x).
            k.Push(Rx(Pi/2)*T(0,0,MalletHead));
            k.Lathe(red,new[]{.17f,-MalletHalf,.188f,-.24f,.198f,-.12f,MalletRadius,0,.198f,.12f,.188f,.24f,.17f,MalletHalf},k.Sides(22),1,0,false);
            for(int s=-1;s<=1;s+=2)
            {
                k.Cylinder(mustard,.207f,s*.215f-.03f,s*.215f+.03f,k.Sides(22),k.Fine?.006f:0);
                if(k.Mid)k.Cylinder(redDark,.2015f,s*.13f-.008f,s*.13f+.008f,k.Sides(22),0);
                // A domed cream face, a red ring painted round it.
                var face=s>0?new[]{.172f,.298f,.155f,.318f,.11f,.331f,0,.338f}:new[]{0,-.338f,.11f,-.331f,.155f,-.318f,.172f,-.298f};
                k.Lathe(cream,face,k.Sides(22),1,0,false);
                if(k.Fine)k.Torus(redDark,V(0,0,s*.326f),.13f,.0045f,k.Sides(22),5);
            }
            k.Pop();
        }
    }
}
