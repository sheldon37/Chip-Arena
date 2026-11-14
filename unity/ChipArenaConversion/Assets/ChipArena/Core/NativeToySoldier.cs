using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Toy Soldier (Puppet Master's summon) as a sculpted tin toy (owner 2026-10-29: "go through the chips that provide visual
    /// changes and update them according to the new models (NO VOXEL appearances)"): our own rounded figure in place of the
    /// voxel "summon-toy" blocks once the player's model is on (the Classic voxel look keeps the blocks). A red coat with white
    /// cross belts, gold buttons, collar and epaulettes; navy trousers with a white stripe; black boots; white gloves; a round
    /// face with dot eyes, rosy cheeks and a moustache under a tall black shako with a gold badge and a red plume; a toy musket
    /// in the right hand. One section per game joint (source frame, each in its joint's frame as the rig rests: CapturedRig's
    /// bones), so the summon's own rig and pose (NativeSummonPose) drive it exactly as they drove the blocks, at the summon's
    /// .6 scale. Presentation only.
    /// </summary>
    public static class NativeToySoldier
    {
        public const string Model="summon-toy-figure";
        private static readonly WeaponPaint Coat=WeaponPaint.Solid(0xc8352e,.12f),Navy=WeaponPaint.Solid(0x1f2a4d,.08f),White=WeaponPaint.Solid(0xf2efe6,.06f),Gold=WeaponPaint.Solid(0xe0b43a,.3f),
            Skin=WeaponPaint.Solid(0xf2c7a5,.04f),Black=WeaponPaint.Solid(0x1a1a1f,.12f),Wood=WeaponPaint.Solid(0x6b4423,.05f),Cheek=WeaponPaint.Solid(0xf08a8a,.03f),Steel=WeaponPaint.Solid(0x9aa4ad,.2f);
        // A cylinder up +y (the sculpt's run along +z turned up) from y0 to y1 at (x, z).
        private static void Up(WeaponSculpt k,in WeaponPaint c,float r,float y0,float y1,int sides,float x=0,float z=0,float bevel=0)
        {
            k.Push(new Matrix4x4(1,0,0,0, 0,0,-1,0, 0,1,0,0, x,0,z,1));k.Cylinder(c,r,y0,y1,sides,bevel);k.Pop();
        }
        // A tapered body of revolution up +y: (radius, y) pairs, rising.
        private static void Turned(WeaponSculpt k,in WeaponPaint c,float[] ry,int sides,float x=0,float z=0)
        {
            k.Push(new Matrix4x4(1,0,0,0, 0,0,-1,0, 0,1,0,0, x,0,z,1));k.Lathe(c,ry,sides);k.Pop();
        }
        /// <summary>The figure: one section per joint it shows on.</summary>
        public static WeaponMesh Build()
        {
            var mesh=new WeaponMesh{Spec="toy-soldier",Tier=0};var k=new WeaponSculpt(mesh,0);
            // Hips and belt (the pelvis; the spine is .105 up, the hips .13 out and .055 down).
            k.Into(CapturedRig.Pelvis);k.Box(Navy,new Vector3(0,-.015f,0),new Vector3(.34f,.15f,.22f),.035f);k.Box(White,new Vector3(0,.055f,0),new Vector3(.355f,.04f,.232f),.012f);k.Cylinder(Gold,.028f,.11f,.122f,10,.004f,0,.055f);
            // The coat's skirt (the spine; the chest .19 up) and its tails behind.
            k.Into(CapturedRig.Spine);k.Box(Coat,new Vector3(0,.085f,0),new Vector3(.37f,.19f,.245f),.04f);k.Box(Coat,new Vector3(0,-.03f,-.085f),new Vector3(.3f,.1f,.06f),.02f);
            // The coat's body (the chest; the neck .32 up, the shoulders .345 out at .18): cross belts, buttons, collar, epaulettes.
            k.Into(CapturedRig.Chest);k.Box(Coat,new Vector3(0,.16f,0),new Vector3(.46f,.33f,.27f),.06f);
            foreach(float side in new[]{-1f,1f})
            {
                k.Push(Matrix4x4.CreateRotationZ(side*.62f)*Matrix4x4.CreateTranslation(0,.16f,.134f));k.Box(White,Vector3.Zero,new Vector3(.05f,.44f,.012f),.004f);k.Pop();
                Up(k,Gold,.062f,.3f,.325f,12,side*.255f,0,.006f);
                for(int i=0;i<5;i++)k.Box(Gold,new Vector3(side*(.255f+.05f),.29f,-.04f+i*.02f),new Vector3(.012f,.035f,.012f),.003f);
            }
            for(int i=0;i<4;i++)k.Cylinder(Gold,.014f,.133f,.143f,8,.003f,0,.05f+i*.07f);
            k.Box(Gold,new Vector3(0,.315f,0),new Vector3(.2f,.045f,.2f),.015f);
            // The neck, then the head: face, dot eyes, rosy cheeks, nose, moustache; the shako, its visor, badge and plume.
            k.Into(CapturedRig.Neck);Up(k,Skin,.055f,-.02f,.1f,10);
            k.Into(CapturedRig.Head);k.Sphere(Skin,new Vector3(0,.1f,0),new Vector3(.135f,.135f,.13f),6,12);
            foreach(float side in new[]{-1f,1f}){k.Sphere(Black,new Vector3(side*.045f,.12f,.12f),new Vector3(.016f,.02f,.012f),3,6);k.Sphere(Cheek,new Vector3(side*.078f,.07f,.105f),new Vector3(.024f,.018f,.012f),3,6);}
            k.Sphere(Skin,new Vector3(0,.09f,.133f),new Vector3(.02f,.02f,.018f),3,6);k.Box(Black,new Vector3(0,.062f,.128f),new Vector3(.07f,.016f,.02f),.007f);
            Turned(k,Black,new[]{.122f,.195f,.126f,.3f,.132f,.42f},14);Up(k,Gold,.134f,.405f,.425f,14,0,0,.004f);
            k.Push(Matrix4x4.CreateRotationX(.18f)*Matrix4x4.CreateTranslation(0,.205f,.1f));k.Box(Black,Vector3.Zero,new Vector3(.2f,.018f,.1f),.008f);k.Pop();
            k.Push(Matrix4x4.CreateTranslation(0,.31f,.128f));k.Gem(Gold,Vector3.Zero,.035f,.02f,6,.5f);k.Pop();
            k.Sphere(Coat,new Vector3(0,.48f,0),new Vector3(.045f,.07f,.045f),4,8);
            // Arms: red sleeves with white cuffs, white gloves.
            foreach(int sh in new[]{CapturedRig.LArm,CapturedRig.RArm})
            {
                k.Into(sh);k.Sphere(Coat,new Vector3(0,-.01f,0),new Vector3(.08f,.08f,.08f),4,10);Up(k,Coat,.072f,-.27f,0,10,0,0,.01f);
                k.Into(sh+1);Up(k,Coat,.066f,-.2f,0,10,0,0,.008f);Up(k,White,.072f,-.245f,-.195f,10,0,0,.008f);
                k.Into(sh+2);k.Sphere(White,new Vector3(0,-.05f,0),new Vector3(.062f,.066f,.058f),4,10);
            }
            // The musket in the right hand, held upright: stock down by the glove, barrel and bayonet up.
            // (In front of the glove and a little out from the sleeve: the right hand's outside is -x.)
            k.Into(CapturedRig.RHand);k.Box(Wood,new Vector3(-.02f,-.14f,.08f),new Vector3(.05f,.24f,.08f),.012f);Up(k,Black,.018f,-.04f,.5f,8,-.02f,.095f);Up(k,Gold,.024f,.08f,.1f,8,-.02f,.095f);
            k.Push(Matrix4x4.CreateTranslation(-.02f,.5f,.095f));Turned(k,Steel,new[]{.012f,0f,.006f,.1f,0f,.16f},6);k.Pop();
            // Legs: navy with a white stripe outside, black boots.
            foreach(int hip in new[]{CapturedRig.LHip,CapturedRig.RHip})
            {
                float side=hip==CapturedRig.LHip?1:-1;
                k.Into(hip);Up(k,Navy,.085f,-.25f,0,10,0,0,.01f);k.Box(White,new Vector3(side*.083f,-.125f,0),new Vector3(.012f,.24f,.03f),.004f);
                k.Into(hip+1);Up(k,Navy,.076f,-.2f,0,10,0,0,.008f);Up(k,Black,.082f,-.25f,-.12f,10,0,0,.01f);
                k.Into(hip+2);k.Box(Black,new Vector3(0,-.04f,.045f),new Vector3(.15f,.1f,.25f),.035f);
            }
            mesh.Sections.RemoveAll(x=>x.Positions.Count==0);return mesh;
        }
    }
}
