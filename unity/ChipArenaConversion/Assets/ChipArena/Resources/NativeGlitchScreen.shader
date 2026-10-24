// The Sheldon Intro's full-screen glitch (rules: Core/NativeSheldonIntro.cs Glitch; drawn by Runtime/NativeSheldonIntroPresentation.cs
// through Runtime/NativeGlitchScreenImage.cs): one pass over the finished frame as the last screen camera's image effect
// (OnRenderImage -> Graphics.Blit), before the IMGUI card. Stages, by _GlC.y (the low graphics tier runs only the first):
//  1. slice offsets (bands of rows torn sideways, a few thin hard tears), the RGB split (red and blue pulled apart, plus a
//     radial chromatic aberration toward the edges) and the blackout;
//  2. scanlines and bright streaks in the glow colour, dead-pixel specks;
//  3. datamosh blocks (a block shows a displaced block of the frame, smeared sideways from its left edge).
// Everything is a pure function of the uniforms (the seed jumps on glitch steps, so the pattern holds between them); the
// pattern is laid out in the uv the frame is sampled at, so a top-down source (D3D) only mirrors it. The block between BEGIN
// SHARED and END SHARED is the whole effect; the offline render harness compiles it as GLSL ES 3.00 (float2 -> vec2, frac,
// lerp, saturate and UNROLL are macros there), so it keeps to the common subset: float literals, no scalar swizzles, fixed
// loops, every texture read through SceneTap (tex2Dlod: no derivatives).
Shader "Hidden/ChipArena/NativeGlitchScreen" {
 Properties { _MainTex("Texture",2D)="black"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize;
   float4 _GlA;    // tear, split, scan, blocks (0..1)
   float4 _GlB;    // black, chroma, noise (0..1), seed (a glitch step)
   float4 _GlC;    // aspect, stages (1..3), screen height (px), unused
   float4 _GlTint; // the glow colour (linear)
   #define UNROLL [unroll]
   float3 SceneTap(float2 uv){return tex2Dlod(_MainTex,float4(saturate(uv),0.0,0.0)).rgb;}
// BEGIN SHARED
float GlH1(float n){return frac(sin(n*12.9898+0.731)*43758.5453);}
float GlH2(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
float GlSmooth(float a,float b,float x){float t=saturate((x-a)/(b-a));return t*t*(3.0-2.0*t);}
float3 Glitch(float2 uv,float2 px)
{
    float tear=_GlA.x;float split=_GlA.y;float scan=_GlA.z;float blocks=_GlA.w;
    float black=_GlB.x;float chroma=_GlB.y;float noise=_GlB.z;float seed=_GlB.w;
    float stages=_GlC.y;float height=max(_GlC.z,1.0);
    float2 p=uv;
    // 1. Slices: bands of rows (their count changes with the seed) torn sideways, and a few thin hard tears.
    float bands=14.0+floor(GlH1(seed*1.7)*26.0);float row=floor(uv.y*bands);
    float shift=0.0;
    if(GlH1(row*3.1+seed*17.3)<tear*0.6)shift=(GlH1(row*7.7+seed*5.1)-0.5)*0.16*tear;
    float thin=floor(uv.y*height/3.0);
    if(GlH1(thin*1.3+seed*9.7)>1.0-tear*0.05)shift+=(GlH1(thin*0.37+seed*2.3)-0.5)*0.3*tear;
    p.x+=shift;
    // 3. Datamosh blocks: a displaced block of the frame, smeared sideways from its left edge.
    if(stages>2.5&&blocks>0.001)
    {
        float2 grid=float2(24.0,14.0);float2 cell=floor(uv*grid);
        if(GlH2(cell+float2(seed*0.37,seed*0.11))<blocks*0.28)
        {
            float2 off=(float2(GlH2(cell+float2(seed+1.3,2.1)),GlH2(cell+float2(seed+7.1,5.9)))-float2(0.5,0.5))*float2(0.25,0.08);
            float2 local=frac(uv*grid);p=(cell+float2(local.x*0.15,local.y))/grid+off;
        }
    }
    // 1. The RGB split (red and blue pulled apart) and the radial chromatic aberration.
    float2 dir=float2((0.004+0.016*split)*split,0.0)+(uv-float2(0.5,0.5))*(0.035*chroma);
    float3 c=float3(SceneTap(p+dir).x,SceneTap(p).y,SceneTap(p-dir).z);
    // 2. Scanlines and bright streaks in the glow colour.
    if(stages>1.5&&scan>0.001)
    {
        float lines=0.5+0.5*sin(uv.y*height*3.14159);c=c*(1.0-0.2*scan*lines);
        UNROLL for(int i=0;i<6;i++)
        {
            float fi=float(i);float y=GlH1(fi*5.3+seed*1.91);float w=(1.0+2.5*GlH1(fi+seed*3.3))/height;float d=abs(uv.y-y);
            float on=step(GlH1(fi*9.1+seed),scan);float x0=GlH1(fi*2.7+seed*0.7)*0.8;float len=0.2+0.6*GlH1(fi*4.1+seed);
            float inside=step(x0,uv.x)*step(uv.x,x0+len);c=c+_GlTint.rgb*(on*inside*(1.0-GlSmooth(0.0,w,d))*0.9*scan);
        }
    }
    // 1. The blackout.
    c=c*(1.0-black);
    // 2. Dead-pixel specks (also in the blackout: the digital silence).
    if(stages>1.5&&noise>0.001)
    {
        float n=GlH2(floor(px/2.0)+float2(seed*13.0,seed*7.0));c=c+float3(0.75,1.0,1.0)*step(1.0-noise*0.03,n);
    }
    return max(c,float3(0.0,0.0,0.0));
}
// END SHARED
   float4 frag(v2f_img i):SV_Target{return float4(Glitch(i.uv,i.pos.xy),1.0);}
   ENDCG
  }
 }
 Fallback Off
}
