// The match camera's bloom (Runtime/NativeBloom.cs; parameters and the CPU mirror of the knee: Core/NativeBloomRules.cs).
// Unity-only: the Electron client drew no bloom (owner effects overhaul, 2026-10-11). Runs as the match camera's first
// image effect (OnRenderImage -> Graphics.Blit), before the damage screen and before the IMGUI HUD, all in linear HDR
// (Linear colour space; the camera's source is an HDR float target and every temporary is ARGBHalf).
//  0 Prefilter: full size -> half. Each of the 13 taps is made safe (negatives, NaN and Inf to 0, then its brightest
//    channel limited to the firefly clamp, hue kept) and thresholded on its brightest channel with a soft knee that is
//    exactly 0 at or below the threshold (linear 1: lit scenery, the white floor grid and every world colour <= 1 never
//    bloom; additive overlaps and HDR emissive do), then box-filtered (13-tap, Jimenez 2014 "Next generation post
//    processing in Call of Duty: Advanced Warfare").
//  1 Down: the same 13-tap filter, half again per level (6 levels in all: half to 1/64 size).
//  2 Up: from the smallest level back to half size, each level plus a 9-tap tent of the smaller result (the dual-filter
//    accumulate: every level weighs 1, so the near levels give the tight glow and the far ones the soft halo).
//  3 Composite: the frame plus a tent of the half-size sum times the intensity (additive), then a hue-keeping display
//    shoulder above 0.9 that whitens only very hot pixels (BloomShoulder; review 2026-10-11: HDR fireball puffs clipped to a
//    white blob). With the bloom off (the low graphics tier) the frame keeps Unity's plain per-channel clip.
// Orientation: Graphics.Blit orients its source, so every single-source pass (0, 1) reads the right way up; the two
// passes that read a second texture (2, 3) flip that texture's v when _MainTex reports a negative texel height (D3D: the
// camera's anti-aliased or back-buffer source stored top-down), Unity's documented rule for a second texture.
// The blocks between SHARED BEGIN and SHARED END are the whole per-pixel effect. An offline harness compiles exactly those
// blocks as GLSL (float2/3/4 -> vec2/3/4, lerp -> mix, saturate -> clamp as macros; SourceTap and BloomTexTap defined by
// the host), so keep them to the common subset: float literals with a decimal point (no exponents), no integers, no
// matrices, no implicit int/float conversions, no texture calls (only the two taps). D3D11 / SM 3.5: no derivatives
// (tex2Dlod), fixed taps.
Shader "Hidden/ChipArena/NativeBloom" {
 Properties { _MainTex("Texture",2D)="black"{} _BloomTex("Bloom",2D)="black"{} }
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;
 sampler2D _BloomTex;float4 _BloomTex_TexelSize;
 float4 _BloomCurve;   // threshold, knee, firefly clamp, unused
 float4 _BloomParams;  // intensity (Reduced effects already applied), unused, unused, unused
 float3 SourceTap(float2 uv){return tex2Dlod(_MainTex,float4(uv,0.0,0.0)).rgb;}
 float3 BloomTexTap(float2 uv){return tex2Dlod(_BloomTex,float4(uv,0.0,0.0)).rgb;}
// SHARED BEGIN
float BloomPeak(float3 c){return max(c.r,max(c.g,c.b));}
// Negatives, NaN (D3D min/max return the other operand) and Inf out, then the brightest channel at most `limit`, hue kept:
// one hot pixel can never flash the screen.
float3 BloomSafe(float3 c,float limit)
{
 c=min(max(c,float3(0.0,0.0,0.0)),float3(65504.0,65504.0,65504.0));
 float m=BloomPeak(c);
 return c*(min(m,limit)/max(m,0.0001));
}
// The soft knee on the brightest channel b: 0 for b <= t, (b - t)^2 / 4k over [t, t + 2k], then the linear excess
// b - t - k (value and slope continuous at t + 2k), carried onto the colour so the hue holds.
float3 BloomKnee(float3 c,float t,float k)
{
 float b=BloomPeak(c);
 float kk=max(k,0.0001);
 float q=clamp(b-t,0.0,2.0*kk);
 q=q*q/(4.0*kk);
 return c*(max(q,b-t-kk)/max(b,0.0001));
}
// One down tap: the prefilter's taps are made safe and thresholded; later levels are already both.
float3 BloomDownTap(float2 uv,float4 curve,float prefilter)
{
 float3 c=SourceTap(uv);
 if(prefilter>0.5)c=BloomKnee(BloomSafe(c,curve.z),curve.x,curve.y);
 return c;
}
// 13-tap box downsample at a destination pixel (d: the source's texel size): the centre 2x2 box at .5, four corner boxes
// at .125 each.
float3 BloomDown(float2 uv,float2 d,float4 curve,float prefilter)
{
 float3 a=BloomDownTap(uv+d*float2(-1.0,-1.0),curve,prefilter);
 float3 b=BloomDownTap(uv+d*float2(0.0,-1.0),curve,prefilter);
 float3 c=BloomDownTap(uv+d*float2(1.0,-1.0),curve,prefilter);
 float3 e=BloomDownTap(uv+d*float2(-0.5,-0.5),curve,prefilter);
 float3 f=BloomDownTap(uv+d*float2(0.5,-0.5),curve,prefilter);
 float3 g=BloomDownTap(uv+d*float2(-1.0,0.0),curve,prefilter);
 float3 h=BloomDownTap(uv,curve,prefilter);
 float3 i=BloomDownTap(uv+d*float2(1.0,0.0),curve,prefilter);
 float3 j=BloomDownTap(uv+d*float2(-0.5,0.5),curve,prefilter);
 float3 k=BloomDownTap(uv+d*float2(0.5,0.5),curve,prefilter);
 float3 l=BloomDownTap(uv+d*float2(-1.0,1.0),curve,prefilter);
 float3 m=BloomDownTap(uv+d*float2(0.0,1.0),curve,prefilter);
 float3 n=BloomDownTap(uv+d*float2(1.0,1.0),curve,prefilter);
 float3 o=(e+f+j+k)*0.125;
 o+=(a+b+g+h)*0.03125;
 o+=(b+c+h+i)*0.03125;
 o+=(g+h+l+m)*0.03125;
 o+=(h+i+m+n)*0.03125;
 return o;
}
// 9-tap tent (1 2 1 / 2 4 2 / 1 2 1) of the bloom texture (d: its texel size).
float3 BloomTent(float2 uv,float2 d)
{
 float3 s=BloomTexTap(uv+d*float2(-1.0,-1.0));
 s+=BloomTexTap(uv+d*float2(0.0,-1.0))*2.0;
 s+=BloomTexTap(uv+d*float2(1.0,-1.0));
 s+=BloomTexTap(uv+d*float2(-1.0,0.0))*2.0;
 s+=BloomTexTap(uv)*4.0;
 s+=BloomTexTap(uv+d*float2(1.0,0.0))*2.0;
 s+=BloomTexTap(uv+d*float2(-1.0,1.0));
 s+=BloomTexTap(uv+d*float2(0.0,1.0))*2.0;
 s+=BloomTexTap(uv+d*float2(1.0,1.0));
 return s*0.0625;
}
// One up step: this level (the source) plus the tent of the smaller result.
float3 BloomUp(float2 uv,float2 bloomUv,float2 d){return SourceTap(uv)+BloomTent(bloomUv,d);}
// The display shoulder on the brightest channel m (NativeBloomRules.Shoulder): unchanged up to 0.9, then eased into 1.0
// (never above it) with the hue kept, so an HDR colour that is only a little over 1 (a fireball puff, a neon edge) shows its
// own hue instead of clipping channel by channel toward yellow-white; from m = 2 it also blends toward white (fully at
// m = 8.25), so the white-hot centres of cores and sparks still read hot.
float3 BloomShoulder(float3 c)
{
 float m=BloomPeak(c);
 float d=0.9+0.1*(1.0-exp((0.9-m)*10.0));
 float w=clamp((m-2.0)*0.16,0.0,1.0);
 float3 t=lerp(c*(d/max(m,0.0001)),float3(d,d,d),w);
 return m>0.9?t:c;
}
// The frame plus the bloom times the intensity, through the display shoulder.
float3 BloomComposite(float3 scene,float2 bloomUv,float2 d,float intensity){return BloomShoulder(scene+BloomTent(bloomUv,d)*intensity);}
// SHARED END
 // The second texture's v (passes 2 and 3): flipped when the blit's source is stored top-down (see the header).
 float2 BloomUv(float2 uv)
 {
  #if UNITY_UV_STARTS_AT_TOP
  if(_MainTex_TexelSize.y<0.0)uv.y=1.0-uv.y;
  #endif
  return uv;
 }
 float4 FragPrefilter(v2f_img i):SV_Target{return float4(BloomDown(i.uv,_MainTex_TexelSize.xy,_BloomCurve,1.0),1.0);}
 float4 FragDown(v2f_img i):SV_Target{return float4(BloomDown(i.uv,_MainTex_TexelSize.xy,_BloomCurve,0.0),1.0);}
 float4 FragUp(v2f_img i):SV_Target{return float4(BloomUp(i.uv,BloomUv(i.uv),_BloomTex_TexelSize.xy),1.0);}
 float4 FragComposite(v2f_img i):SV_Target
 {
  float4 scene=tex2Dlod(_MainTex,float4(i.uv,0.0,0.0));
  return float4(BloomComposite(scene.rgb,BloomUv(i.uv),_BloomTex_TexelSize.xy,_BloomParams.x),scene.a);
 }
 ENDCG
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass { // 0 Prefilter
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment FragPrefilter
   #pragma target 3.5
   ENDCG
  }
  Pass { // 1 Down
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment FragDown
   #pragma target 3.5
   ENDCG
  }
  Pass { // 2 Up
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment FragUp
   #pragma target 3.5
   ENDCG
  }
  Pass { // 3 Composite
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment FragComposite
   #pragma target 3.5
   ENDCG
  }
 }
 Fallback Off
}
