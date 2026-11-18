using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The ride (docs/EXPEDITION-OVERHAUL.md R7 and W3): one braced second in first person inside my own falling pod during the hub's
    /// opening countdown, the red-lit cabin round me and the city rushing up in the door's porthole, between the sky shot and the
    /// chase. Presentation only and engine-free: a window on the opening's clock (the pod's drop age, NativePodDrop) and the camera's
    /// pose in the pod's own frame (Unity's: base at the origin, doorway toward +z), with the brace's judder. It keeps today's 3 s
    /// opening and Astra's 5 s one alike (the opening is read from the drop's own Appear; the plan's -4.1 s beat at 5 s, -2.6 s at
    /// 3 s, ending before any pod lands). Camera shake off: the same beat held steady; Reduced effects: no cabin moment at all.
    /// Runtime/NativePodRidePresentation.cs places my camera; the pod countdown's directed camera (NativeCinematicCamera) yields to
    /// it while Active.
    /// </summary>
    public static class NativePodRide
    {
        /// <summary>The opening the pods' drop plays (s): -NativePodDrop.Appear (3 today; Astra's ExpeditionRules.PodOpening, 5 for a
        /// generated hub, once the drop reads it).</summary>
        public static float Opening=>-NativePodDrop.Appear;
        /// <summary>Whether the opening is the long one (5 s) the plan's beats are timed for.</summary>
        public static bool Long(float opening)=>opening>=4.5f;
        /// <summary>When the ride starts (s from the fight start, negative: in the countdown) and how long it lasts.</summary>
        public static float RideAt(float opening)=>-opening+(Long(opening)?.9f:.4f);
        public static float RideSeconds(float opening)=>Long(opening)?1f:.8f;
        /// <summary>The ride holds at drop age `age` (s from the fight start) for an opening of `opening` s; `u` its share (0..1).</summary>
        public static bool Active(float age,float opening,out float u)
        {
            u=0;if(!(age>=-60)||!(opening>0))return false;float at=RideAt(opening),len=RideSeconds(opening);if(age<at||age>=at+len)return false;
            u=(age-at)/len;return true;
        }
        /// <summary>The seated eye in the pod (strapped in at the back of the cabin, facing the door) and where it looks (the door's
        /// porthole, a little down at the city coming up).</summary>
        public static readonly Vector3 Eye=new Vector3(0,NativePodModels.Floor+1.12f,-.12f),Look=new Vector3(0,-.2f,1);
        /// <summary>The brace's judder at drop age `age` (`u` the ride's share): an offset in the pod's frame (m) and a pitch and roll
        /// (degrees): a hard shudder as the cabin buffets, easing as it starts and ends. None with camera shake off.</summary>
        public static void Judder(float age,float u,bool shake,out Vector3 offset,out float pitch,out float roll)
        {
            offset=Vector3.Zero;pitch=roll=0;if(!shake)return;float k=(float)Math.Sin(Math.PI*Math.Max(0,Math.Min(1,u)));k=.35f+.65f*k;
            double t=age*1.0;
            offset=new Vector3((float)(Math.Sin(t*71)*.5+Math.Sin(t*43+1.3)*.5)*.006f*k,(float)(Math.Sin(t*89+.4)*.6+Math.Sin(t*37)*.4)*.009f*k,0);
            pitch=(float)(Math.Sin(t*53+2)*.6+Math.Sin(t*29)*.4)*.7f*k;roll=(float)(Math.Sin(t*61+.7)*.5+Math.Sin(t*23+2.1)*.5)*1.1f*k;
        }
        /// <summary>The cabin's red on my screen while the ride holds (0..1): in with the cut, out at its end.</summary>
        public static float Red(float u)=>Math.Max(0,Math.Min(1,Math.Min(u/.12f,(1-u)/.18f)));
    }
}
