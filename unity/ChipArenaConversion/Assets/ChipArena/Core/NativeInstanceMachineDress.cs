using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // What the machines' corners look like in the generated instances (docs/EXPEDITION-INSTANCE-MACHINES.md): a PvE machine is a clean sci-fi cabinet, and these levels aren't. Each gets a little dressing round its feet and a light of its own on the wall over it, so it
    // reads from down a corridor even when the circuit overhead is dead, and looks as if someone wheeled it in. Sculpted with the same kit, in a machine's model frame (origin on the floor at its spot, +z its front; its wall about .72 m behind). Presentation only: flat on the floor or up on the wall, nothing here collides.
    public static class NativeInstanceMachineDress
    {
        public const string BackroomsId="machine-dress-backrooms",PoolroomsId="machine-dress-poolrooms";
        public const int Body=0,Lights=1;
        /// <summary>The bank's models.</summary>
        public static readonly (string id,Func<WeaponMesh> build,int budget)[] All={(BackroomsId,BuildBackrooms,1200),(PoolroomsId,BuildPoolrooms,1500)};
        /// <summary>A map's dressing, or null.</summary>
        public static string For(string map)=>NativeBackroomsGenerator.IsMap(map)?BackroomsId:NativePoolroomsGenerator.IsMap(map)?PoolroomsId:null;
        /// <summary>The light over the machine: a level the draw tints, steady in the Poolrooms, buzzing in Level 0 (a bad tube on a slow flicker, keyed to the machine so they don't agree).</summary>
        public static float Lamp(string map,float t,int machine)
        {
            if(!NativeBackroomsGenerator.IsMap(map))return 1.1f;float k=(float)(Math.Sin(t*.73f+machine*2.1f)+Math.Sin(t*1.9f+machine));return k>1.55f?.45f+.3f*(float)Math.Abs(Math.Sin(t*41)):1.05f;
        }
        private const float Wall=-.71f,Up=-(float)(Math.PI/2);
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static WeaponPaint Lit(int hex,float k=1){var c=WeaponPaint.Linear(hex)*k;return new WeaponPaint(c,c,0);}
        private static WeaponMesh New(string id,Action<WeaponSculpt> draw){var mesh=new WeaponMesh{Spec=id,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);draw(s);return mesh;}
        // The bracket and tube on the wall over the machine, its cable down the wall behind it.
        private static void WallLamp(WeaponSculpt s,in WeaponPaint metal,in WeaponPaint cable,int tube,float width)
        {
            s.Box(metal,V(0,2.82f,Wall+.03f),V(width+.1f,.05f,.06f),.008f);foreach(int side in new[]{-1,1})s.Box(metal,V(side*(width/2+.03f),2.8f,Wall+.05f),V(.03f,.08f,.08f),.004f);
            s.Into(Lights);s.Push(WeaponSculpt.Ry((float)Math.PI/2));s.Cylinder(Lit(tube,1.5f),.024f,-width/2,width/2,8,0,-(Wall+.07f),2.78f);s.Pop();s.Into(Body);
            var path=new List<Vector3>{V(width/2+.03f,2.8f,Wall+.015f),V(.62f,2.6f,Wall+.015f),V(.74f,2.0f,Wall+.015f),V(.72f,1.2f,Wall+.015f),V(.76f,.5f,Wall+.015f),V(.76f,.08f,Wall+.03f),V(.62f,.02f,Wall+.14f)};
            s.Tube(cable,path,.011f,6,Vector3.UnitZ);for(int k=0;k<4;k++)s.Box(metal,V(.73f+.01f*(k%2),.5f+k*.62f,Wall+.022f),V(.05f,.018f,.012f));
        }
        private static WeaponMesh BuildBackrooms()=>New(BackroomsId,s=>
        {
            // The carpet's damp round its feet, darker where it has been standing; the bad tube over it; a note taped to the wall nobody has read.
            var damp=WeaponPaint.Solid(0x5d5532);var stain=WeaponPaint.Solid(0x463f22);
            s.Push(WeaponSculpt.S(1,.84f,1)*WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.004f,.42f));s.Cylinder(damp,1.02f,0,.002f,18,0,0,0);s.Cylinder(stain,.72f,.002f,.003f,14,0,.08f,-.06f);s.Pop();
            WallLamp(s,WeaponPaint.Solid(0x8d8a70,.02f),WeaponPaint.Solid(0x1a1a18),0xfff0c4,.86f);
            s.Box(WeaponPaint.Solid(0xe8e0b8),V(-.9f,1.46f,Wall+.003f),V(.22f,.3f,.004f));s.Box(WeaponPaint.Solid(0x35312a),V(-.9f,1.54f,Wall+.006f),V(.16f,.02f,.002f));for(int k=0;k<3;k++)s.Box(WeaponPaint.Solid(0x6e6850),V(-.9f,1.47f-k*.04f,Wall+.006f),V(.16f-.02f*k,.01f,.002f));
            s.Box(WeaponPaint.Solid(0x9c9476),V(-.9f,1.615f,Wall+.006f),V(.07f,.024f,.002f));
        });
        private static WeaponMesh BuildPoolrooms()=>New(PoolroomsId,s=>
        {
            // A rubber mat on the tiles, a wet-floor sign, its own white tube on a galvanised bracket; the cable in a plastic run down the wall.
            var mat=WeaponPaint.Solid(0x1d3a3e);var rib=WeaponPaint.Solid(0x2a4e52);
            s.Box(mat,V(0,.006f,.42f),V(1.5f,.012f,1.5f),.002f);for(int k=0;k<9;k++)s.Box(rib,V(0,.013f,-.2f+k*.155f),V(1.42f,.004f,.05f));
            WallLamp(s,WeaponPaint.Solid(0xb4bcc0,.04f),WeaponPaint.Solid(0xd8dcdc),0xd8fbff,.86f);
            var yellow=WeaponPaint.Solid(0xe8b81c,.04f);var black=WeaponPaint.Solid(0x101014);
            // The sign: two leaves leaning on each other, a black figure's stand-in, a bar for its words.
            float x=1.0f,z=.42f;foreach(int side in new[]{-1,1})
            {
                s.Push(WeaponSculpt.Rx(side*.2f)*WeaponSculpt.T(x,.3f,z+side*.06f));s.Box(yellow,V(0,0,0),V(.3f,.6f,.012f),.004f);foreach(int f in new[]{-1,1}){s.Box(black,V(0,.18f,f*.0075f),V(.2f,.03f,.003f));s.Box(black,V(0,-.01f,f*.0075f),V(.03f,.18f,.003f));s.Box(black,V(0,-.04f,f*.0075f),V(.12f,.03f,.003f),0,0,0,.6f);}s.Pop();
            }
            s.Box(WeaponPaint.Solid(0x80d0d8,.06f),V(-.82f,.002f,.72f),V(.5f,.003f,.32f),0,0,.4f,0);s.Box(WeaponPaint.Solid(0x80d0d8,.06f),V(-.98f,.002f,.48f),V(.22f,.003f,.2f),0,0,.9f,0);
        });
    }
}
