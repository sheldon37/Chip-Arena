#ifndef NATIVE_MUZZLE_LIGHT_INCLUDED
#define NATIVE_MUZZLE_LIGHT_INCLUDED
// The guns' muzzle lights (Core/NativeMuzzleLight.cs; Runtime/NativeMuzzleLights.cs uploads them every arena frame): four
// slots of their own, apart from the map's eight practical slots and the blast flashes, so every map has them (the old maps,
// the dressed maps, the zones and the town alike). ChipArena/NativeBlockLit adds them to its light block, ChipArena/SourceLit,
// ChipArena/ZombieLit and ChipArena/NativeLit add them as emission (albedo times the light), so the floor, the walls, the kit,
// the pilots, the horde, the station's kinds and the first-person gun and hands all take the same flash.
//  _MuzzleLightPos[k]: xyz the light (world), w its reach (m). Unused slots sit far below.
//  _MuzzleLightColor[k]: rgb linear colour times intensity, w the softening radius near the light (m: the light falls off as
//  d^2 / (d^2 + w^2) close in, so a gun or a wall at the muzzle never burns white).
//  The light: colour x (1 - (d/reach)^2)^2 x d^2 / (d^2 + w^2) x 1 / (1 + MUZZLE_FALL (d/reach)^2) x the wrapped N.L.
//  _MuzzleLightCount: the slots in use; 0 (also when unset) skips the block.
// ---- MUZZLE LIGHT SHARED BEGIN (HLSL and GLSL: float literals only; NativeMuzzleLight.Shade is its CPU copy) ----
// NativeMuzzleLight.Fall: the inverse-square share, 1 / (1 + Fall (d / reach)^2), so the pool is hot by the gun and fades by
// a third of the reach before the window (1 - (d / reach)^2)^2 takes it to 0 at the reach.
#define MUZZLE_FALL 8.0
float4 _MuzzleLightPos[4];float4 _MuzzleLightColor[4];float _MuzzleLightCount;
float3 MuzzleLights(float3 world,float3 n,float wrap)
{
    float3 sum=float3(0.0,0.0,0.0);
    if(_MuzzleLightCount<0.5)return sum;
    for(int k=0;k<4;k++)
    {
        float3 d=_MuzzleLightPos[k].xyz-world;float len2=max(dot(d,d),0.000001);float len=sqrt(len2);
        float q=len/max(_MuzzleLightPos[k].w,0.0001);float a=saturate(1.0-q*q);float w=_MuzzleLightColor[k].w;
        float soft=len2/(len2+w*w);float fall=1.0/(1.0+q*q*MUZZLE_FALL);float ndl=saturate((dot(n,d/len)+wrap)/(1.0+wrap));
        sum+=_MuzzleLightColor[k].rgb*(a*a*soft*fall*ndl);
    }
    return sum;
}
// ---- MUZZLE LIGHT SHARED END ----
#endif
