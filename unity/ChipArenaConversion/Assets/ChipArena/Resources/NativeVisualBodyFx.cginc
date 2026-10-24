#ifndef NATIVE_VISUAL_BODY_FX_INCLUDED
#define NATIVE_VISUAL_BODY_FX_INCLUDED
// The visual body's hit flash and match-start spawn-in (docs/BOZO-INTEGRATION.md step 4), shared by its toon
// (NativeVisualBodyToon.shader: the body and its outline) and its hologram (NativeVisualBodyGhost.shader). The arena writes
// them per renderer each frame a pilot draws (Runtime/NativeVisualBodyPresentation.cs VisualBodyLook, from the same flash
// code and spawn-in frame the voxel parts read). Unset, every value leaves the body as it is.
//  _HitFlash:    the struck pilot's flash code (Core NativeHitFlash.Code: 1 none, 1 + k a hit at strength k, 3 + k a headshot)
//  _SpawnFront:  x the solid front's world height (the build front less its lag; far above while no spawn-in builds), y the
//                band, z the heat span, w the hologram slice's lead past the solid front (Core NativeSpawnIn's constants)
//  _SpawnTint:   rgb the hardlight tint, w its share (the frame's tint)
//  _SpawnGlitch: x the completion flash, y the RGB split's offset (m), z the split copies' opacity, w the scan clock
//  _SpawnHolo:   rgb the hologram's colour (linear), w the slice's opacity (0 on the low graphics tier)
float _HitFlash;float4 _SpawnFront;float4 _SpawnTint;float4 _SpawnGlitch;float4 _SpawnHolo;
// ---- HIT FLASH SHARED BEGIN (HLSL and GLSL: float literals only) ----
float3 HitFlashColor(float k,float head)
{
 float3 red=head>0.5?float3(1.0,0.16,0.1):float3(1.0,0.42,0.32);
 float w=smoothstep(0.0,1.0,(k-(head>0.5?0.45:0.2))/(head>0.5?0.55:0.8));
 return lerp(red,float3(1.0,1.0,1.0),w)*(head>0.5?2.6:1.6);
}
float HitFlashBlend(float k,float head,float rim)
{
 return min(1.0,k*(head>0.5?0.95:0.82)*(0.82+0.18*rim));
}
// ---- HIT FLASH SHARED END ----
// SourceLit's flash: the lit colour pulled toward white through the damage red, a little more on the silhouette (rim 1).
float3 VisualHitFlash(float3 c,float rim)
{
 if(_HitFlash<1.001)return c;
 float head=_HitFlash>2.5?1.0:0.0;float k=saturate(_HitFlash-(head>0.5?3.0:1.0));
 return lerp(c,HitFlashColor(k,head),HitFlashBlend(k,head,rim));
}
// Core NativeSpawnIn.Solid, Heat and Ghost at a pixel's world height `y` instead of a part's.
float SpawnSolid(float y){float t=saturate((_SpawnFront.x-y)/_SpawnFront.y);return t*t*(3.0-2.0*t);}
float SpawnHeat(float y){return 1.0-saturate((_SpawnFront.x-_SpawnFront.y-y)/_SpawnFront.z);}
float SpawnSlice(float y){return saturate((_SpawnFront.x+_SpawnFront.w-y)/_SpawnFront.y)*(1.0-SpawnSolid(y));}
// The voxel parts' dithered reveal (SourceLit): a world cell hash against the solid share. The hash is floored above 0:
// at world-sized arguments it resolves to about 1/256, and a cell hashing to 0 would otherwise draw where nothing has formed
// (SourceLit skips an unformed part whole, so it never meets that cell).
void SpawnClip(float3 world)
{
 float s=SpawnSolid(world.y);
 if(s<0.999)clip(s-max(frac(sin(dot(floor(world*120.0),float3(12.9898,78.233,37.719)))*43758.5453),0.001));
}
// The spawn-in's hardlight tint (SpawnTintOf): the tint colour by its share, hottest where the body has just formed, then
// the completion flash toward white.
float3 SpawnTinted(float3 c,float y)
{
 float k=saturate(_SpawnTint.w*(0.35+0.65*SpawnHeat(y)))*0.8;
 float3 t=lerp(float3(1.0,1.0,1.0),_SpawnTint.rgb,k);
 return c*lerp(t,float3(2.2,2.3,2.5),_SpawnGlitch.x*0.7);
}
// The dead pilot's dissolve (Runtime/NativeVisualBodyPresentation.cs VisualCorpseShown and VisualSceneShown, as the corpse
// fades): _Dissolve 0 (whole) to 1 (gone) across the noise the bake carries (_VisualDissolveNoise, global: the owner's
// dissolve pack), read on the part's own texture coordinates, so the cells stay on the cloth as the ragdoll moves. The
// front's band glows _DissolveEdge (linear, the pilot's accent). At 0 nothing is read or cut. Returns the band's share.
sampler2D _VisualDissolveNoise;float _Dissolve;float4 _DissolveEdge;
float DissolveFront(float2 uv)
{
 if(_Dissolve<=0.0)return 0.0;
 float n=tex2D(_VisualDissolveNoise,uv*3.0).r,cut=_Dissolve*1.08;
 clip(n-cut);
 return 1.0-saturate((n-cut)/0.07);
}
#endif
