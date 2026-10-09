using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private readonly NativeViewSway viewSway=new NativeViewSway();
        // first-person-view.ts:71-80 view layer on the steady view root: look sway, stride bob, landing kick, gun recoil, reload
        // dip, run-low carry and rocket/mark kicks. The ADS sight alignment has already been measured on the steady pose, so the
        // layer rides on top of the aligned root as in the source. Inputs are the presented pilot (host state, or the replicated
        // snapshot on a guest) and the local camera's look; the presentation clock drives it, so pause and slow motion hold it.
        private void ApplyViewSway(Actor shown,float time,float ads)
        {
            var view=ViewCamera.transform;Vector3 forward=view.forward;
            var pose=viewSway.Sample(NativeViewSway.Input(shown,time,NativeViewSway.SourceYaw(forward.x,forward.z),NativeViewSway.SourcePitch(forward.y),ads));
            var root=viewRig.Root.transform;
            root.SetPositionAndRotation(root.position+view.rotation*new Vector3(pose.X,pose.Y,pose.UnityZ),root.rotation*Quaternion.Euler(pose.UnityPitchDegrees,0,pose.UnityRollDegrees));
        }
    }
}
