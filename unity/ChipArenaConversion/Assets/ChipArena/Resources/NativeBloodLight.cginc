#ifndef NATIVE_BLOOD_LIGHT_INCLUDED
#define NATIVE_BLOOD_LIGHT_INCLUDED
// The blood pack's light (docs/BLOOD-PACK.md): what ChipArena/NativeBloodSplash, NativeBloodDecal and NativeBloodGut share.
// Blood is lit by the scene's ambient (the SH), the key light (ForwardBase) and the arena's own light block (the eight
// practical slots NativeLighting writes, the three blast flashes and the muzzle lights), so it sits in the same light as the
// floor it lands on; its wet surface takes a highlight from the key and from the strongest practical light at the vertex.
float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];float4 _ArenaFlashPos[3];float4 _ArenaFlashColor[3];
#include "NativeMuzzleLight.cginc"
// The practical lights at a point: their sum softly wrapped round `n` (wrap 1: no facing at all), and the strongest one's
// direction and colour (for the highlight). Per vertex.
float3 BloodArenaLights(float3 world,float3 n,float wrap,out float3 domDir,out float3 domColor)
{
    float3 sum=float3(0.0,0.0,0.0);domDir=float3(0.0,1.0,0.0);domColor=float3(0.0,0.0,0.0);float best=0.0;
    for(int k=0;k<8;k++)
    {
        float3 d=_ArenaLightPos[k].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float a=saturate(1.0-q*q);
        float3 c=_ArenaLightColor[k].rgb*(a*a);float3 l=d/len;sum+=c*saturate((dot(n,l)+wrap)/(1.0+wrap));
        float lum=dot(c,float3(0.3,0.6,0.1));if(lum>best){best=lum;domDir=l;domColor=c;}
    }
    for(int m=0;m<3;m++)
    {
        float3 d=_ArenaFlashPos[m].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaFlashPos[m].w,0.0001);q*=q;
        sum+=_ArenaFlashColor[m].rgb*(saturate(5.0*(1.0-q))/(1.0+25.0*q)*saturate((dot(n,d/len)+wrap)/(1.0+wrap)));
    }
    sum+=MuzzleLights(world,n,wrap);
    return sum;
}
// A wet highlight: a tight lobe for the glint and a broad one (`sheen`) for the sheen, for a light from `l` seen along `v`.
float BloodGlint(float3 n,float3 v,float3 l,float gloss,float sheen)
{
    float ndh=saturate(dot(n,normalize(l+v)));float tight=pow(ndh,40.0+140.0*gloss);float broad=pow(ndh,10.0);
    return (tight*(0.6+1.6*gloss)+broad*sheen)*saturate(dot(n,l)*4.0+0.4);
}
#endif
