using System;
namespace ChipArena.UnityConversion
{
    public static class NativePveParity
    {
        public static float AddImpulse(float impulse)=>Math.Min(6,Math.Max(0,impulse)/80);
        public static float MedicHeal(int medics,float maxHp,bool underFire){float total=0;for(int i=0;i<medics;i++)total+=3*(float)Math.Pow(.5,i);return Math.Min(total,maxHp*.04f)*(underFire?.6f:1);}
    }
}
