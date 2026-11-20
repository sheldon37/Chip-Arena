using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The look of the v2 fallback void: a few colours (linear rgb) and levels an intro tints to its own palette.</summary>
    public struct NativeIntroVoidLook
    {
        /// <summary>The far colour (background and fog), the floor, the light pool under the pilot, the rim.</summary>
        public Vector3 Far,Floor,Pool,Rim;
        /// <summary>The key light's level (1 normal) and the pool's level.</summary>
        public float Key,PoolLevel;
        public static NativeIntroVoidLook Of(Vector3 accent)=>new NativeIntroVoidLook{Far=NativeEntranceFx.Hex(0x04050a),Floor=NativeEntranceFx.Hex(0x0b0d14),Pool=Vector3.Lerp(NativeEntranceFx.Hex(0x9fb4ff),accent,.35f),Rim=accent,Key=1,PoolLevel=1};
    }

    /// <summary>
    /// The v2 fallback set (docs/INTROS-V2.md "Sets"): a dark lit void, never the old block arena. A vast floor that fades into the
    /// fog, a soft pool of light under the pilot and drifting motes for depth (Motes); a key, a rim and a cool fill. (An unlit horizon
    /// glow read as a hard band in the first capture: none.)
    /// Built from the source-model primitives every build has, so a build without the set banks still looks deliberate.
    /// Pure and allocation-free.
    /// </summary>
    public static class NativeIntroVoid
    {
        public const int Pieces=5;
        public static void Draw(INativeIntroSet set,float t,in NativeIntroVoidLook look)
        {
            float floor=NativeEntranceStage.FloorY;
            // The floor: a wide, very flat disc (its edge lost in the fog), and a darker skirt under it so no horizon line shows.
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,floor-.02f,0),Quaternion.Identity,new Vector3(60,.04f,60),look.Floor);
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,floor-.6f,0),Quaternion.Identity,new Vector3(140,1,140),look.Far*.6f);
            // The pool of light where the pilot stands (glow, layered so its edge is soft), breathing very slowly.
            float breathe=.92f+.08f*(float)Math.Sin(t*.9f);
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,floor+.002f,0),Quaternion.Identity,new Vector3(3.4f,.002f,3.4f),look.Pool*(.035f*look.PoolLevel*breathe));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,floor+.003f,0),Quaternion.Identity,new Vector3(1.9f,.002f,1.9f),look.Pool*(.05f*look.PoolLevel*breathe));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,floor+.004f,0),Quaternion.Identity,new Vector3(.9f,.002f,.9f),look.Pool*(.07f*look.PoolLevel*breathe));
        }
        /// <summary>The void's light: fog and background in the far colour, a low ambient, a soft key, a rim behind, a cool fill.</summary>
        public static void Light(NativeEntranceLights l,float t,in NativeIntroVoidLook look,Vector3 chest)
        {
            l.Clear();l.Background=look.Far;l.Fog=look.Far;l.FogNear=6;l.FogFar=26;l.DriveProbe=true;
            l.Sky=NativeEntranceFx.Hex(0x6f7f9f,.28f);l.Ground=NativeEntranceFx.Hex(0x120f18,.35f);
            l.Directional(new Vector3(2.5f,5,4),NativeEntranceFx.Hex(0xdfe8ff,.75f*look.Key));
            l.Add(chest+new Vector3(-.5f,1.1f,-1.9f),look.Rim*(6.5f*look.Key),8);
            l.Add(chest+new Vector3(2.4f,.4f,1.6f),NativeEntranceFx.Hex(0x7d9cff,1.6f*look.Key),7);
            l.Add(new Vector3(0,.25f,0),look.Pool*(1.2f*look.PoolLevel),3.5f);
        }
        /// <summary>Drifting motes (depth for a moving camera), thinned by quality; at most 28 glows.</summary>
        public static void Motes(INativeEntranceFx fx,float t,float quality,Vector3 tint,int seed=4100)
        {
            for(int i=0;i<28;i++)
            {
                if(NativeEntranceMath.Rand(seed,i+400)>Math.Max(.2f,quality))continue;
                var p=new Vector3(NativeEntranceMath.Signed(seed,i*3)*5,.15f+2.8f*NativeEntranceMath.Rand(seed,i*3+1),NativeEntranceMath.Signed(seed,i*3+2)*5);
                p+=new Vector3((float)Math.Sin(t*.25f+i)*.2f,(float)Math.Sin(t*.17f+i*1.7f)*.15f+t*.03f,(float)Math.Cos(t*.21f+i*.6f)*.2f);
                fx.Glow(p,tint*(.05f+.04f*(float)Math.Sin(t*.9f+i*2.3f)),0,.03f,0,0,seed+i);
            }
        }
    }
}
