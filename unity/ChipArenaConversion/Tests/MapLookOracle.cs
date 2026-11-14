#if ARENA_TESTS
using System;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The lighting state NativeLighting.Create then NativeLighting.Apply(map) left behind at ce3c506, before the map-id
    /// branches moved into Resources/MapLooks.json: each value transcribed expression by expression from that commit
    /// (Runtime/NativeLighting.cs:66-71 and Create, plus the constants NativeNightSky.shader hard-coded), with Unity's
    /// Color maths mirrored in float (ColorOf, Color.Lerp, Color * float; alpha kept). Tests/map-looks-parity.json is this
    /// oracle's output for the 15 maps, written once at ce3c506 and never regenerated; MapLookChecks compares the oracle to
    /// it and the new data to both. `RimFrom` is Create's rim position: no Apply touched the rim direction (the draft stage
    /// turns it, and nothing turned it back; see NativeLighting's rim note).
    /// </summary>
    internal sealed class OldMapLight
    {
        public string id,clear,fogMode,ambientMode,keyShadows,rimShadows;public bool fog,skybox;
        public float farClip,fogStart,fogEnd,moonSize,keyIntensity,keyStrength,rimIntensity,haloFalloff,stars,cloudSpeed,shadowDistance;public int shadowCascades;
        public float[] background,fogColor,horizon,zenith,keyColor,keyFrom,rimColor,rimFrom,ambientSky,ambientEquator,ambientGround,sunDir,halo,disc,discShade,cloud;
    }
    internal static class OldMapLighting
    {
        internal static float[] ColorOf(int value)=>new[]{((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,1f};
        internal static float[] Lerp(float[] a,float[] b,float t){t=t<0?0:t>1?1:t;return new[]{a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t,a[3]+(b[3]-a[3])*t};}
        internal static float[] Mul(float[] a,float b)=>new[]{a[0]*b,a[1]*b,a[2]*b,a[3]*b};
        internal static OldMapLight Evaluate(ContentMap map)
        {
            var s=new OldMapLight{id=map.id};
            // Create: ambient, fog mode, shadows, the two lights' fixed settings.
            s.ambientMode="trilight";s.ambientSky=Mul(ColorOf(0x92b4f0),.48f);s.ambientEquator=Mul(ColorOf(0x9aaccd),.27f);s.ambientGround=Mul(ColorOf(0x171320),.48f);
            s.fog=true;s.fogMode="linear";s.shadowDistance=65;s.shadowCascades=2;
            s.keyIntensity=.9f;s.keyShadows="soft";s.keyStrength=1;s.rimFrom=new[]{14f,7f,12f};s.rimIntensity=.24f;s.rimShadows="none";
            // Apply.
            bool indoor=map.id=="siren",cold=map.theme=="moonlit"||map.theme=="snow",fire=map.id=="forge";s.farClip=350;s.background=ColorOf(indoor?0x050407:map.sky);s.clear=indoor?"solid":"skybox";
            s.fogColor=indoor?ColorOf(0x0d0a12):Lerp(ColorOf(map.sky),ColorOf(0x405677),.42f);s.fogStart=indoor?30:65;s.fogEnd=indoor?118:210;
            s.horizon=ColorOf(fire?0x633345:cold?0x405677:0x333f64);s.zenith=ColorOf(fire?0x100b1c:0x080e22);s.moonSize=map.id=="moonlit"?.065f:.028f;s.skybox=!indoor;
            s.keyColor=ColorOf(map.id=="outpost"?0xffc5a5:fire?0xffac9c:0xb2ceff);s.keyFrom=indoor?new[]{-3f,30f,-5f}:new[]{-8f,20f,-12f};s.rimColor=ColorOf(map.accent);
            // NativeNightSky.shader's constants: moon direction (normalised in the shader), halo colour and falloff, disc colour
            // and crater shade, the star field at full strength, the lit cloud tint and the drift speed.
            s.sunDir=new[]{-.4f,.46f,-.78f};s.halo=new[]{.16f,.21f,.3f};s.haloFalloff=12;s.disc=new[]{.79f,.86f,1f};s.discShade=new[]{.76f,.24f};s.stars=1;s.cloud=new[]{.24f,.29f,.40f};s.cloudSpeed=1;
            return s;
        }
    }
}
#endif
