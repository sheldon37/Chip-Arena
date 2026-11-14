using System;

namespace ChipArena.UnityConversion
{
    // The match camera's bloom (Runtime/NativeBloom.cs drives Resources/NativeBloom.shader): its parameters, the CPU mirror of
    // the shader's soft knee (BloomKnee), the level plan and the quality gate, checked by Tests/BloomChecks.cs. Unity-only, a
    // deliberate deviation from the Electron client (three.js drew no post effects; owner effects overhaul, 2026-10-11):
    // cosmetic, screen space, the same on host, guests and killcam, and no saved preference of its own.
    //  * Threshold 1, knee .5 on the brightest linear channel: exactly 0 at or below 1, so lit scenery, the white floor grid
    //    lines and every world colour <= 1 never bloom; a quadratic knee over [1, 2], then the linear excess b - 1.5 above
    //    2: additive overlaps that clear 1 start glowing softly and HDR emissive glows in full. No parity fixture covers a
    //    screen post effect, so this needs no exporter owner cut.
    //  * FireflyClamp 10: each source tap's brightest channel is limited to 10 (hue kept; negatives, NaN and Inf go to 0
    //    first), so a single hot pixel adds a few tenths to its neighbours and nothing 60 px away instead of flashing.
    //  * Levels 6, from half size down to 1/64 (1920x1080: 960x540 .. 30x16): the halo spans the same share of the screen
    //    at any resolution. Each up step adds every level at weight 1 (the dual-filter accumulate), so the near levels give
    //    a tight glow and the far ones the soft halo.
    //  * Shoulder (the composite's last step, review 2026-10-11: the HDR fireball puffs clipped channel by channel to a
    //    white blob, and stacked additive glows to yellow-white): on the brightest channel m, unchanged up to ShoulderStart
    //    .9, then eased to at most 1 (d = .9 + .1 (1 - e^(-(m - .9) / .1))) with the hue kept, so a colour a little over 1
    //    shows its own hue; from WhiteFrom 2 it also blends toward white (weight (m - 2) x WhiteRate .16, full at 8.25), so
    //    the white-hot centres of cores and sparks still read hot. Only while the bloom runs; the low tier keeps the clip.
    //  * Intensity .4: picked from offline renders of the shader's shared code (compiled as GLSL) on a synthetic 1080p HDR
    //    frame (a 2.6 neon tube, a glow core of 4, sparks of 6, a 3.0 fireball 180 px across, the floor grid at 1): .6 hazed
    //    the busy frame, .3 left the neon edge at +.3; .4 adds .41 at the tube's edge, .1 8 px out and under .01 past 64 px,
    //    and nothing on the grid. Reduced effects x.6 (.24); off on the low graphics tier (Settings · Graphics performance,
    //    or auto on a small or SM < 4.5 GPU).
    public static class NativeBloomRules
    {
        public const float Threshold=1f,Knee=.5f,FireflyClamp=10f,Intensity=.4f,ReducedScale=.6f,ShoulderStart=.9f,ShoulderSoft=.1f,WhiteFrom=2f,WhiteRate=.16f;
        public const int Levels=6,MinLevelSize=2;
        // The shader's BloomKnee on a brightest channel b: what is left of it after the threshold (the colour is scaled by
        // Weight(b) / b).
        public static float Weight(float b)
        {
            float k=Math.Max(Knee,.0001f),q=Math.Min(Math.Max(b-Threshold,0),2*k);q=q*q/(4*k);
            return Math.Max(q,b-Threshold-k);
        }
        // The shader's BloomShoulder on a linear colour: unchanged while its brightest channel is at most ShoulderStart,
        // otherwise scaled to the eased peak d and blended toward white (d, d, d) by the white weight.
        public static System.Numerics.Vector3 Shoulder(System.Numerics.Vector3 c)
        {
            float m=Math.Max(c.X,Math.Max(c.Y,c.Z));if(!(m>ShoulderStart))return c;
            float d=ShoulderStart+ShoulderSoft*(1-(float)Math.Exp((ShoulderStart-m)/ShoulderSoft)),w=Math.Min(1,Math.Max(0,(m-WhiteFrom)*WhiteRate));
            return System.Numerics.Vector3.Lerp(c*(d/Math.Max(m,.0001f)),new System.Numerics.Vector3(d,d,d),w);
        }
        // Down level `level` (0: the prefilter's half size) of a side `size` pixels long.
        public static int LevelSize(int size,int level)=>level<0||level>=31?0:Math.Max(0,size)>>(level+1);
        // How many levels a width x height source gets: at most Levels, every level at least MinLevelSize on both sides.
        public static int LevelCount(int width,int height)
        {
            int n=0;while(n<Levels&&LevelSize(width,n)>=MinLevelSize&&LevelSize(height,n)>=MinLevelSize)n++;
            return n;
        }
        // The composite intensity: 0 on the low graphics tier (the component is disabled there), x ReducedScale with Reduced effects.
        public static float Strength(bool lowGraphics,bool reducedEffects)=>lowGraphics?0:Intensity*(reducedEffects?ReducedScale:1);
    }
}
