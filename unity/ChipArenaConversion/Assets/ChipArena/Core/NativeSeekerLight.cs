namespace ChipArena.UnityConversion
{
    /// <summary>
    /// client/siren/fx.ts at 05950ea: every Marked seeker carries PointLight(0xf1ecff, 4, 9, 2) at its centre (makeShot), its
    /// intensity set to 3 + 2·pulse each frame with pulse = .5 + .5·sin(9t) (updateShots). Ward Zero leaves effect lights at
    /// their own level. The runtime keeps one pooled Unity light per live seeker, up to <see cref="Cap"/>, converted as every
    /// other pooled source light (NativeBlastSim.UnityLightIntensity, range = distance, sRGB colour).
    /// </summary>
    public static class NativeSeekerLight
    {
        public const int Color=0xf1ecff,Cap=4;
        public const double Created=4,Distance=9,Decay=2,Base=3,Swing=2;
        /// <summary>The three.js intensity at time t (the seeker's halo and core pulse on the same clock).</summary>
        public static double Intensity(double time)=>Base+Swing*NativeSirenFx.ShotPulse(NativeSirenShotKind.Seeker,time);
    }
}
