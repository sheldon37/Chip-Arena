using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Bombadier's TNT (owner 2026-11-04: "holding TNTs instead of a grenade launcher"; original geometry; docs/BOMBADIER.md): a bundle of three sticks in each fist, the grip along +z through the
    // wrist as the blades' (the fist round black cloth tape at z = 0). Red paper with crimped ends, two more bands of tape, a cream label on each stick with its stencil, and a length of fuse from a brass
    // crimp out of the top, ending in an ember (the sparks are drawn live: NativeTechniqueFx.HeldFuse). Built on both hands, the left a mirror image.
    public static partial class NativeWeaponModels
    {
        /// <summary>The bundle's fuse tip in the weapon frame: where its sparks leave, and the socket anything asking for this weapon's 'muzzle' finds.</summary>
        public static readonly Vector3 TntFuseTip=new Vector3(.018f,.03f,.335f);
        static partial void Bombs(Dictionary<string,Design> d)
        {
            d[NativeBombs.Spec]=new Design{Spec=NativeBombs.Spec,Build=Tnt,Dual=true,MirrorLeft=true,Muzzle=TntFuseTip};
        }
        private static void Tnt(WeaponSculpt k)
        {
            const float R=.021f,Spread=.0235f,From=-.125f,To=.215f,Wrap=Spread+R+.0018f;
            var paper=Paint(0xb3241b,.04f);var tape=Paint(0x17181b,.05f);var label=Paint(0xdccfa4,.01f);var ink=Paint(0x16120e);var brassy=Paint(Brass,.18f);var fuse=Paint(0x2b2118,.01f);var ember=Paint(0xff7a1c,.2f);
            // Three sticks in a triangle, crimped at both ends.
            for(int j=0;j<3;j++)
            {
                double a=j*Math.PI*2/3+Math.PI/2;float cx=Spread*(float)Math.Cos(a),cy=Spread*(float)Math.Sin(a);
                k.Push(T(cx,cy,0));k.Lathe(paper,new[]{.009f,From,R-.005f,From+.003f,R,From+.012f,R,To-.012f,R-.005f,To-.003f,.009f,To},k.Sides(12),1,0,true);k.Pop();
                if(!k.Mid)continue;
                // The label, facing out: cream paper, and on the full model its stencil, along the stick.
                var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);var along=V(0,0,1);var across=Vector3.Cross(n,along);var on=V(cx,cy,0)+n*R;const float At=.174f;
                k.Decal(label,on+along*At,along*.03f,across*.0092f);
                if(k.Fine)
                {
                    const float W=.0165f,H=.0125f;
                    void TeeAt(float z){var c=on+along*z;k.Decal(ink,c+across*(H*.42f),along*(W*.36f),across*(H*.09f),.002f);k.Decal(ink,c,along*(W*.1f),across*(H*.5f),.002f);}
                    TeeAt(At-W);TeeAt(At+W);
                    var mid=on+along*At;k.Decal(ink,mid-along*(W*.32f),along*(W*.09f),across*(H*.5f),.002f);k.Decal(ink,mid+along*(W*.32f),along*(W*.09f),across*(H*.5f),.002f);k.Decal(ink,mid,along*(W*.09f),across*(H*.5f)-along*(W*.24f),.002f);
                }
            }
            // The tape: the grip under the fist, and a band each side of it.
            k.Cylinder(tape,Wrap,-.052f,.062f,k.Sides(14),.002f);
            k.Cylinder(tape,Wrap,.118f,.14f,k.Sides(14),.001f);
            k.Cylinder(tape,Wrap,-.098f,-.08f,k.Sides(14),.001f);
            // The fuse, out of a brass crimp in the middle of the top, ending in its ember.
            k.Cylinder(brassy,.011f,To-.012f,To+.012f,k.Sides(8),.002f);
            k.Tube(fuse,new List<Vector3>{V(0,0,To+.008f),V(.002f,.006f,To+.038f),V(.009f,.02f,To+.074f),V(TntFuseTip.X,TntFuseTip.Y,TntFuseTip.Z-.006f)},.0042f,k.Sides(5),Vector3.UnitY,1,true);
            k.Sphere(ember,TntFuseTip,V(.0062f,.0062f,.0062f),k.Sides(4),k.Sides(6));
        }
    }
}
