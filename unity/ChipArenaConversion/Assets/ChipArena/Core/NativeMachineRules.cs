using System;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeMachineUse {public int machine,owner;public string result;public float at;}
    public static class NativeMachineRules
    {
        public static float Flap(float age)=>MathUtil.Smooth(age/.15f)*(1-MathUtil.Smooth((age-.65f)/.2f));
        public static float Door(float age)=>MathUtil.Smooth(age/.4f)*(1-MathUtil.Smooth((age-1.4f)/.5f));
        public static float Deny(float age)=>age>=0&&age<.55f?(1-age/.55f)*((int)Math.Floor(age*10)%2==1?.4f:1):0;
        public static float Activity(float age,bool med){float duration=med?2:.6f;return age>=0&&age<duration+.3f?1-MathUtil.Smooth((age-duration)/.3f):0;}
        // Where a placed machine (PveContent machines) is drawn: its model root is the authority's spot on the machine's own floor
        // (the upper floor's dispensers and med stations stand at y 6), turned by its yaw, so the model's front (+z, the slot and
        // screen side) faces the authority's front point (spot + .7 m along the yaw). The town shares the Ward's content but the
        // authority builds and serves machines only in PvE rounds and Expedition instances (NativeArena.BuildPveMachines,
        // TryExpeditionInteract), so nothing is drawn or labelled there.
        /// <summary>Whether the machines stand in this arena: every PvE round, and an Expedition area only in a recognized instance.</summary>
        public static bool Present(string mapId,bool expedition)=>!expedition||NativeMapRules.WardInstance(mapId)||NativeMapRules.ExpeditionInstance(mapId);
        /// <summary>A model-space point (Unity frame: +z the front, +y up) of a placed machine, in the world.</summary>
        public static V3 World(PveMachine m,float x,float y,float z){float s=(float)Math.Sin(m.yaw),c=(float)Math.Cos(m.yaw);return new V3(m.x+x*c+z*s,m.y+y,m.z-x*s+z*c);}
        /// <summary>The model's root: the machine's spot on its own floor.</summary>
        public static V3 Root(PveMachine m)=>new V3(m.x,m.y,m.z);
        /// <summary>The chip slot a dispensed chip leaves (NativeMachineModels.Slot) and the price label's anchor over the machine.</summary>
        public static V3 Slot(PveMachine m)=>World(m,0,1.02f,.27f);
        public static V3 Label(PveMachine m)=>World(m,0,2.62f,0);
        public static (bool visible,V3 position,float spin,float progress) Pickup(V3 from,V3 rest,float born,float time)
        {float start=born-.55f;if(time<start)return(false,from,0,0);if(time<born){float k=MathUtil.Clamp((time-start)/.55f,0,1),e=1-(1-k)*(1-k);return(true,new V3(from.x+(rest.x-from.x)*e,from.y+(rest.y-from.y)*k+1.1f*(float)Math.Sin(Math.PI*k),from.z+(rest.z-from.z)*e),(time-start)*2.2f*4,k);}return(true,new V3(rest.x,rest.y+.08f*(float)Math.Sin(time*2),rest.z),born*2.2f*4+(time-born)*2.2f,1);}
    }
}
