using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // first-person-view.ts: critically damped ADS blend, source offsets and FOV counter-scale.
    public sealed class NativeAimMotion
    {
        private float at=-100,velocity;public float Weight {get;private set;}
        public void Reset(){at=-100;velocity=0;Weight=0;}
        public float Sample(float time,bool aiming)
        {
            float dt=time-at,target=aiming?1:0,value=Weight;
            if(dt<0||dt>.35f){value=target;velocity=0;}
            else NativeFirstPersonPose.Spring(ref value,ref velocity,target,26,1,Math.Min(.05f,dt));
            at=time;return Weight=MathUtil.Clamp(value,0,1);
        }
    }
    public static class NativeAimPresentation
    {
        public static Vector3 Offset(WeaponId weapon)=>weapon==WeaponId.Pistol?new Vector3(-.27f,.19f,0):weapon==WeaponId.MachineGun?new Vector3(-.31f,.175f,0):new Vector3(-.33f,.10f,0);
        public static float Scale(float fov,float blend)=>NativeFirstPersonPose.Scale*(1+((float)(Math.Tan(fov*Math.PI/360)/Math.Tan(NativeViewRules.FirstFov*Math.PI/360))-1)*MathUtil.Clamp(blend,0,1));
        public static bool Scope(bool first,bool aiming,WeaponId weapon,float blend)=>first&&aiming&&weapon==WeaponId.Rifle&&blend>.6f;
        // CSS scope-overlay at 33vh, soft edge to 33.3vh; radius is measured in screen-height units.
        public static float ScopeAlpha(float radius)=>.75f*MathUtil.Clamp((radius-.33f)/.003f,0,1);
    }
}
