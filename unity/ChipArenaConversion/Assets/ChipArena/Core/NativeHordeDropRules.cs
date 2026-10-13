using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public static class NativeHordeDropRules
    {
        public const float DamageCentre=10,DamageRim=5,Impulse=380,AddImpulse=200,Pop=2.5f;
        public static bool UsesDrop(string kind,PveEntry[] entries)=>kind=="brute"&&entries.Any(e=>NativeHordeDrop.Is(e)&&!e.hulk);
        // Rotate only after a successful selection; a full drop queue never redirects a brute to a door.
        public static PveEntry Next(PveEntry[] entries,ref int cursor,Func<PveEntry,bool> available)
        {
            var drops=entries.Where(e=>NativeHordeDrop.Is(e)&&!e.hulk).ToArray();
            for(int i=0;i<drops.Length;i++)
            {int at=(cursor+i)%drops.Length;if(!available(drops[at]))continue;cursor=(at+1)%drops.Length;return drops[at];}
            return null;
        }
        public static float Exposed(PveEntry entry,float walk,float active)=>NativeHordeDrop.Is(entry)?NativeHordeDrop.LandAt(active):entry.kind=="vent"?active-1.15f:walk;
        public static float TargetAt(PveEntry entry,float active)=>NativeHordeDrop.Is(entry)?NativeHordeDrop.LandAt(active):active;
        public static bool Held(PveEntry entry,float now,float active)=>NativeHordeDrop.Is(entry)&&now<NativeHordeDrop.LandAt(active);
        public static bool Touch(V3 delta)=>Math.Abs(delta.y)<1.5f&&delta.x*delta.x+delta.z*delta.z<=NativeHordeDrop.SlamRadius*NativeHordeDrop.SlamRadius;
        public static float Damage(float distance,bool blocked)=>blocked?0:DamageCentre+(DamageRim-DamageCentre)*MathUtil.Clamp(distance/NativeHordeDrop.SlamRadius,0,1);
        public static V3 Outward(V3 delta,V3 fallback)
        {var flat=new V3(delta.x,0,delta.z);return flat.Length>.0001f?flat.Normalized:new V3(fallback.x,0,fallback.z).Normalized;}
        public static V3 Shove(V3 delta,V3 fallback,float impulse,bool blocked)
            =>(Outward(delta,fallback)*(impulse/65f)+new V3(0,Pop,0))*(blocked?.5f:1);
    }
}
