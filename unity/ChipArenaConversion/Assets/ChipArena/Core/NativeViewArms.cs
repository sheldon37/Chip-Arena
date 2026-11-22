using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The pilot's own arms in first person for every weapon the tactical view does not hold (owner 2026-11-22: "Go ahead
    /// and fix", after F1's first-person audit: the hands sat below the frame for most blades and staffs, the greatsword's left
    /// forearm ended in an open sleeve). The old view model keeps its clips, its hands and its weapon where they are; the view
    /// body's shoulders go to an eye-anchored place (the pack's first-person shoulders), the arms reach the hands by two-bone IK
    /// and are cut at the clavicles, so they run in from the screen's edges. Each class's hold rises into view by its Lift
    /// (camera frame, m), measured on the view at rest. Presentation only: aim, reach, hit volumes, timing and the wire are the
    /// game's.</summary>
    public static class NativeViewArms
    {
        /// <summary>The shoulders' midpoint in the eye frame at true size (the pack's first-person rifle and pistol shoulders).</summary>
        public static readonly Vector3 ShoulderAnchor=new Vector3(0,-.12f,-.17f);
        /// <summary>Each elbow's pole in the eye frame at true size: down and out, as the pack's elbows hang.</summary>
        public static readonly Vector3 PoleRight=new Vector3(.3f,-.42f,-.02f),PoleLeft=new Vector3(-.3f,-.42f,-.02f);

        /// <summary>Whether `spec` takes the eye-anchored arms: every melee, magic, bow, bomb and fighter class, but not the guns
        /// (the tactical view), the Siren (its voxel arms), the Top Dog and the Cyber Lord (they read as they are).</summary>
        public static bool Eligible(string spec)
        {
            if(string.IsNullOrEmpty(spec))return false;
            if(spec=="pistol"||spec=="rifle"||spec=="machinegun"||spec=="siren"||spec==NativeTopDog.Spec||spec==CyberLordSpec.Id)return false;
            return true;
        }

        /// <summary>The class's hold raised into view (camera frame, m, at the old view's scale), measured on its view at rest
        /// so both hands and the grip show; zero where the hold already shows.</summary>
        public static Vector3 Lift(string spec)
        {
            switch(spec)
            {
                default:return Vector3.Zero;
            }
        }
    }
}
