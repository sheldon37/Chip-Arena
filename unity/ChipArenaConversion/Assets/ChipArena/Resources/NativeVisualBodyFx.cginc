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
// A directed entrance's per-bone reveal (Runtime/NativeVisualBodyPresentation.cs VisualRevealLook: the voxel parts'
// partOpacity, the Sheldon Intro's glitch drop-outs): each game bone a world segment, _RevealA[k].xyz to _RevealB[k].xyz,
// and its solid share (_RevealA[k].w). A pixel takes its nearest segment's share and is dithered by it in SpawnClip's world
// cells, so a bone of the model drops out or flickers in as the voxel part does. With _RevealOn 0 nothing is read or cut.
float _RevealOn;float4 _RevealA[21];float4 _RevealB[21];
void RevealClip(float3 world)
{
 if(_RevealOn<0.5)return;
 float best=1e9;float share=1.0;
 for(int k=0;k<21;k++)
 {
  float3 a=_RevealA[k].xyz,ab=_RevealB[k].xyz-a;float t=saturate(dot(world-a,ab)/max(dot(ab,ab),1e-6));float3 d=world-(a+ab*t);
  float e=dot(d,d);if(e<=best){best=e;share=_RevealA[k].w;}
 }
 if(share<0.999)clip(share-max(frac(sin(dot(floor(world*120.0),float3(12.9898,78.233,37.719)))*43758.5453),0.001));
}
// The pilot's skin tone (Core NativeLook's skin entry; Runtime/NativeVisualBodyPresentation.cs VisualSkinToneOne, written on the
// body's skin parts only): _SkinTone rgb the picked tone and w on, _SkinRef rgb the part's base skin (its palette's brightest
// skin colour), both in the colour space the shader works in. A pixel that reads as skin (Core NativeColorWheel.SkinShare,
// tested on its sRGB colour) is scaled by the tone over the base per channel, so the shade, the blush and the lips keep their
// relation to the skin; any other pixel keeps its colour. With _SkinTone.w 0 (unset, or no tone picked) nothing changes.
float4 _SkinTone;float4 _SkinRef;
float SkinShare(float3 c)
{
 #ifndef UNITY_COLORSPACE_GAMMA
 c=sqrt(max(c,float3(0.0,0.0,0.0)));
 #endif
 float hi=max(c.r,max(c.g,c.b)),lo=min(c.r,min(c.g,c.b));float sat=(hi-lo)/max(hi,0.0001);
 float warm=saturate((c.r-c.g)*60.0+0.5)*saturate((c.g-c.b)*60.0+0.5);
 return warm*smoothstep(0.05,0.12,sat)*(1.0-smoothstep(0.80,0.92,sat))*smoothstep(0.03,0.08,hi);
}
float3 SkinToned(float3 c)
{
 if(_SkinTone.w<0.5)return c;
 return lerp(c,saturate(c*(_SkinTone.rgb/max(_SkinRef.rgb,float3(0.0003,0.0003,0.0003)))),SkinShare(c));
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
#ifdef VISUAL_BODY_CUT
// A model corpse's cut (Runtime/NativeVisualCutPresentation.cs; owner 2026-10-28: "slice right on the trail of a hit"), no mesh
// copied: _CutTex holds, per vertex of the body's skinned meshes (texel _CutBase + the vertex's index, in rows _CutWidth long),
// two keep values; _CutPick picks one, (1,0) or (0,1). A split corpse's halves each keep their side of the blade's plane (r for
// half 0, g for half 1: the vertex's signed distance from it in the death pose, positive on that half's side, or 1 and -1 where
// its body stays whole on half 0); a body severed at its joints keeps each vertex all of whose weight is on one piece (r: 1, else
// -1); the death broadcast's bust its bust bones'. The value is interpolated, so a pixel under 0 lies past the cut. With
// _CutPick (0,0) (unset) nothing is read or cut. (The surface shader's analysis pass reads no texture objects.)
float _CutBase;float _CutWidth;float4 _CutPick;
#ifdef SHADER_TARGET_SURFACE_ANALYSIS
float CutKeep(uint vid){return 1.0;}
#else
Texture2D<float2> _CutTex;
float CutKeep(uint vid)
{
 if(_CutPick.x+_CutPick.y<0.5)return 1.0;
 uint i=(uint)_CutBase+vid;uint w=max((uint)_CutWidth,1u);
 return dot(_CutTex.Load(int3(i%w,i/w,0)).rg,_CutPick.xy);
}
#endif
#endif
#endif
