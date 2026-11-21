using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The lobby chair under the model portraits' seated pose (docs/VOXEL-RETIREMENT.md V3 Stage 1, U5; owner 2026-10-29: "NO VOXEL
    // appearances"): the eight boxes of NativePortraitRules.Chair (kept for the voxel portraits) as one modelled armchair of the
    // same size and colours: a padded seat and back with rounded corners and soft edges, tapered round legs, padded armrests on
    // round posts and a frame rail under the seat. One section in the source frame (+x the chair's left, y up, its front +z),
    // built once into the source-model bank (Runtime/NativeBodyChipModels.cs) and queued where the boxes were. Presentation only.
    public static class NativeLobbyChair
    {
        public const string Model="lobby-chair";
        private const int Pad=0x6a4851,Seam=0x573a42,Frame=0x303744,Trim=0xe6a878;
        /// <summary>The seat's top over the floor, the back's front plane, the armrests' height (m; the boxes' own).</summary>
        public const float SeatTop=.385f,BackFront=-.225f,ArmTop=.6325f;
        public static WeaponMesh Build()
        {
            var mesh=new WeaponMesh{Spec="lobby-chair",Tier=0};var k=new WeaponSculpt(mesh,0);k.Into(-1);
            WeaponPaint pad=WeaponPaint.Solid(Pad,.02f),seam=WeaponPaint.Solid(Seam,.01f),frame=WeaponPaint.Solid(Frame,.03f),trim=WeaponPaint.Solid(Trim,.05f);
            // (A slab's outline lies in its xy plane and it runs along its z: Rx(-90°) lays it down, its z up.)
            var down=WeaponSculpt.Rx(-(float)Math.PI/2);
            // The seat: a padded cushion on a thin seam, rounded at its corners.
            k.Push(down*WeaponSculpt.T(0,.30f,0));k.Slab(_=>seam,seam,seam,Rounded(.34f,.31f,.07f),0,.012f,.004f);k.Pop();
            k.Push(down*WeaponSculpt.T(0,.31f,0));k.Slab(_=>pad,pad,pad,Rounded(.33f,.30f,.065f),0,SeatTop-.31f,.03f);k.Pop();
            // The back: a padded panel standing on the seat's rear edge.
            k.Push(WeaponSculpt.T(0,0,-.31f));k.Slab(_=>pad,pad,pad,Offset(Rounded(.33f,.42f,.07f),0,.795f),0,.085f,.03f);k.Pop();
            // The legs: tapered round posts, a little splayed.
            foreach(float x in new[]{-.29f,.29f})foreach(float z in new[]{-.24f,.24f})
            {k.Push(down*WeaponSculpt.T(x,0,z));k.Lathe(frame,new[]{.021f,0f,.025f,.012f,.029f,.27f,.03f,.3f},12,2);k.Pop();}
            // The frame rail under the seat.
            k.Box(frame,new Vector3(0,.285f,0),new Vector3(.62f,.03f,.54f),.012f);
            // The armrests: padded bars on round posts, front and back.
            foreach(float x in new[]{-.37f,.37f})
            {
                k.Box(trim,new Vector3(x,.60f,0),new Vector3(.09f,.05f,.55f),.022f);
                k.Box(pad,new Vector3(x,.6275f,0),new Vector3(.075f,.012f,.5f),.006f);
                foreach(float z in new[]{-.17f,.19f}){k.Push(down*WeaponSculpt.T(x,.30f,z));k.Cylinder(frame,.02f,0,.28f,10,.004f);k.Pop();}
            }
            return mesh;
        }
        /// <summary>A rounded rectangle (counter-clockwise, xy): half sizes `hx` and `hy`, corner radius `r`.</summary>
        public static Vector3[] Rounded(float hx,float hy,float r,int steps=6)
        {
            r=Math.Min(r,Math.Min(hx,hy)*.95f);var o=new Vector3[4*(steps+1)];int n=0;
            float[] cx={hx-r,-(hx-r),-(hx-r),hx-r},cy={hy-r,hy-r,-(hy-r),-(hy-r)};
            for(int c=0;c<4;c++)for(int i=0;i<=steps;i++){double a=(c*90+i*90.0/steps)*Math.PI/180;o[n++]=new Vector3(cx[c]+r*(float)Math.Cos(a),cy[c]+r*(float)Math.Sin(a),0);}
            return o;
        }
        private static Vector3[] Offset(Vector3[] outline,float x,float y){var o=new Vector3[outline.Length];for(int i=0;i<o.Length;i++)o[i]=outline[i]+new Vector3(x,y,0);return o;}
    }
}
