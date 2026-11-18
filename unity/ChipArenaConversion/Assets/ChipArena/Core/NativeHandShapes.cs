using System;
namespace ChipArena.UnityConversion
{
    // The install's hand shapes on the draft stage's model (NativeInstallShot; presentation only): each finger's curl as a share of
    // the rig's Closed curl and the thumb's turn toward the index (NativeVisualRig.HandShape), read by eye off the owner's pack frames
    // (Armed-Pickup-Right at .20 s open, at .63 s the pickup's pinch, Armed-Shield-Activate at .58 s two fingers out, Armed-Activate-Left
    // at .93 s at rest, the pickup's right hand at .40 s cupped; mirrored for the left), so the hand that takes the cartridge pinches it
    // between the thumb and two fingers, presses it in with two fingers, flicks open and rests, and the receiving hand cups, braces
    // and closes into a fist. Only the draft actor wears them (Weight 0 everywhere else: the grip's curl as before). Blends run on the
    // pose clock.
    public static class NativeHandShapes
    {
        public static readonly NativeVisualRig.HandShape Open=new NativeVisualRig.HandShape(.10f,.10f,.06f,.06f,.10f,.12f),
            Pinch=new NativeVisualRig.HandShape(.50f,.45f,.42f,.48f,.80f,.90f),
            Press=new NativeVisualRig.HandShape(.30f,.20f,.10f,.12f,.82f,.92f),
            Flick=new NativeVisualRig.HandShape(.05f,0,.05f,.05f,.05f,.05f),
            Rest=new NativeVisualRig.HandShape(.25f,.10f,.35f,.35f,.35f,.35f),
            Cup=new NativeVisualRig.HandShape(.30f,.15f,.45f,.45f,.45f,.45f);
        /// <summary>The pinch's squeeze past its shape at the catch (settled by PinchSettled).</summary>
        public const float Squeeze=.05f;
        /// <summary>The hand that takes the cartridge (the left for a pilot) at pose age `p`: Open from the lock, Pinch from .52 (a 5%
        /// squeeze settling by .64), Press from the thumb's let-go (1.08-1.11), the Flick at the release (1.15-1.19) and Rest by 1.34.
        /// Before the pick (p < 0) no shape.</summary>
        public static NativeVisualRig.HandShape Left(float p)
        {
            if(!(p>=0))return default;
            if(p<NativeInstallShot.PinchStart)return NativeVisualRig.HandShape.Lerp(Rest,Open,NativeInstallShot.Ramp(p,0,.10f));
            if(p<NativeInstallShot.ThumbOff)
            {
                var s=NativeVisualRig.HandShape.Lerp(Open,Pinch,NativeInstallShot.Ramp(p,NativeInstallShot.PinchStart,NativeInstallShot.Catch));
                float sq=Squeeze*NativeInstallShot.Bump((p-NativeInstallShot.Catch+.02f)/(NativeInstallShot.PinchSettled-NativeInstallShot.Catch+.02f));
                s.Thumb+=sq;s.Index+=sq;s.Middle+=sq;return s;
            }
            if(p<NativeInstallShot.Release)return NativeVisualRig.HandShape.Lerp(Pinch,Press,NativeInstallShot.Ramp(p,NativeInstallShot.ThumbOff,NativeInstallShot.PressShaped));
            if(p<1.22f)return NativeVisualRig.HandShape.Lerp(Press,Flick,NativeInstallShot.Ramp(p,NativeInstallShot.Release,NativeInstallShot.ReleaseEnd));
            return NativeVisualRig.HandShape.Lerp(Flick,Rest,NativeInstallShot.Ramp(p,1.22f,1.34f));
        }
        /// <summary>The receiving hand (the right): Rest, cupped by .40, braced (grip .55) over the line-up, tightening to .85 under the
        /// press, a fist (grip 1, clench .85) as the light leaves the device, resting again over the settle. `grip` and `clench` go on
        /// the grasp; the shape (Cup) is weighted out as the grip takes over. `settle` 0..1: the settle's progress.</summary>
        public static NativeVisualRig.HandShape Right(float p,float settle,out float grip,out float clench)
        {
            grip=0;clench=0;if(!(p>=0))return default;
            float cup=NativeInstallShot.Ramp(p,NativeInstallShot.RiseStart,.40f),brace=NativeInstallShot.Ramp(p,NativeInstallShot.CarryEnd,NativeInstallShot.AlignEnd),fist=NativeInstallShot.Ramp(p,NativeInstallShot.SurgeStart,1.33f);
            grip=.55f*brace+.30f*NativeInstallShot.Ramp(p,NativeInstallShot.Contact,NativeInstallShot.Seat)+.15f*fist;clench=.85f*fist;
            float rest=MathUtil.Clamp(settle,0,1);grip*=1-rest;clench*=1-rest;
            var s=NativeVisualRig.HandShape.Lerp(Rest,Cup,cup);s.Weight=Math.Max(rest,1-brace);if(rest>0)s=NativeVisualRig.HandShape.Lerp(s,Rest,rest);s.Weight=Math.Max(rest,1-brace);return s;
        }
    }
}
