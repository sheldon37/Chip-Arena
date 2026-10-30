#ifndef NATIVE_TOON_SURFACE_INCLUDED
#define NATIVE_TOON_SURFACE_INCLUDED
// The anime map style's lighting model for the surface shaders (ChipArena/NativeLit: the old maps' blocks, trims and gore;
// ChipArena/SourceLit: pilots, monsters, machines, weapons and the source-model scenery such as Brickfall's skyline).
// `#pragma surface <surf> ArenaToon` in place of Lambert. While the style is off (_ArenaToon.x 0: every map without it, a
// stage, the draft, the rooftop) both functions are Unity's Lambert ones exactly. With it on, in the base pass the key (the
// brightest directional, its shadow folded into gi.light.color as Unity's surface shaders do) goes through the ramp over the
// flattened SH (NativeToon.cginc ToonDiffuse); in an add pass every other pixel light (the rim, a blast or seeker light)
// goes on in stairs (ToonPoints). The shader's own point lights (actor and muzzle lights) are stepped where it adds them.
#include "NativeToon.cginc"
inline fixed4 LightingArenaToon(SurfaceOutput s,UnityGI gi)
{
    if(_ArenaToon.x<0.5)return LightingLambert(s,gi);
    float3 lc=_LightColor0.rgb;float atten=saturate(dot(gi.light.color,float3(1.0,1.0,1.0))/max(dot(lc,float3(1.0,1.0,1.0)),0.0001));
    float ndl=dot(s.Normal,gi.light.dir);fixed4 c;c.a=s.Alpha;
    #ifdef UNITY_PASS_FORWARDBASE
    c.rgb=s.Albedo*ToonDiffuse(ToonAmbient(gi.indirect.diffuse,ShadeSH9(float4(0.0,1.0,0.0,1.0)),ShadeSH9(float4(0.0,-1.0,0.0,1.0)),s.Normal.y),lc,ndl,atten);
    #else
    c.rgb=s.Albedo*ToonPoints(lc*(saturate(ndl)*atten));
    #endif
    return c;
}
inline void LightingArenaToon_GI(SurfaceOutput s,UnityGIInput data,inout UnityGI gi){LightingLambert_GI(s,data,gi);}
#endif
