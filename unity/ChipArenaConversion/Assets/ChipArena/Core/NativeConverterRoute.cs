using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    public static class NativeConverterRoute
    {
        // The hall and casting floor are joined by the enlarged, boss-opened shutter.
        public static bool Contains(float x,float z)=>x>=-42&&x<=-28&&Math.Abs(z)<=7
            ||x>=-42&&x<=-12&&Math.Abs(z)<=.8f
            ||x>=-14&&x<=30&&z>=-9&&z<=10;
        public static Vector3 Goal(Vector3 at,Vector3 target)
        {
            bool east=target.X>-24;
            if(east&&at.X<-14)return Math.Abs(at.Z)>.3f&&at.X<-28?new Vector3(-34,at.Y,0):new Vector3(-13,at.Y,0);
            if(!east&&at.X>-28)return Math.Abs(at.Z)>.3f&&at.X>-14?new Vector3(-13,at.Y,0):new Vector3(-34,at.Y,0);
            return east?new Vector3(Math.Max(-13,Math.Min(30,target.X)),at.Y,Math.Max(-9,Math.Min(10,target.Z)))
                :new Vector3(Math.Max(-42,Math.Min(-28,target.X)),at.Y,Math.Max(-7,Math.Min(7,target.Z)));
        }
    }
}
