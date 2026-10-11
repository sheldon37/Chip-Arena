// Effects overhaul, base layer (owner 2026-10-11): bloom for the match camera's HDR image (Runtime/NativeBloomImage.cs,
// an OnRenderImage effect through Graphics.Blit only). The HDR effect glows (NativeVfx and NativeVfxLit colours above 1)
// spill into a soft halo instead of clipping flat at the back buffer. Pass 0: prefilter (a soft-knee threshold on a
// 4-tap Karis-weighted box, so a lone bright pixel does not flicker); 1: 4-tap box downsample; 2: 4-tap box upsample
// added onto the level above (Blend One One); 3: composite, the scene plus the bloom times _BloomIntensity. The composite
// samples two textures, so on a D3D-like target with a flipped source (negative _MainTex_TexelSize.y) the bloom's uv is
// flipped to match, as Unity's image effects do. No tone mapping: the scene is unchanged below the threshold.
Shader "Hidden/ChipArena/NativeBloom"
{
    Properties
    {
        _MainTex("Source",2D)="black"{}
        _BloomTex("Bloom",2D)="black"{}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex,_BloomTex;
    float4 _MainTex_TexelSize;
    float4 _BloomCurve; // threshold, knee, 1/(4 knee), intensity
    // ---- BLOOM SHARED BEGIN (HLSL and GLSL: float literals only, no matrices, no implicit conversions) ----
    // The soft-knee threshold: 0 below threshold - knee, a quadratic knee, then linear above the threshold.
    float3 BloomPrefilter(float3 c, float threshold, float knee, float invKnee)
    {
        float bright = max(c.r, max(c.g, c.b));
        float soft = clamp(bright - threshold + knee, 0.0, 2.0 * knee);
        soft = soft * soft * invKnee;
        float contribution = max(soft, bright - threshold) / max(bright, 0.00001);
        return c * contribution;
    }
    // Karis weighting: a tap's weight falls with its brightness, so a lone very bright pixel does not dominate its block.
    float BloomKaris(float3 c)
    {
        return 1.0 / (1.0 + max(c.r, max(c.g, c.b)));
    }
    // ---- BLOOM SHARED END ----
    float3 Tap(float2 uv){return min(tex2D(_MainTex,uv).rgb,float3(64.0,64.0,64.0));}
    float4 Box(float2 uv,float d)
    {
        float4 o=_MainTex_TexelSize.xyxy*float4(-d,-d,d,d);
        float3 s=Tap(uv+o.xy)+Tap(uv+o.zy)+Tap(uv+o.xw)+Tap(uv+o.zw);
        return float4(s*0.25,1.0);
    }
    float4 fragPrefilter(v2f_img i):SV_Target
    {
        float4 o=_MainTex_TexelSize.xyxy*float4(-1.0,-1.0,1.0,1.0);
        float3 a=Tap(i.uv+o.xy),b=Tap(i.uv+o.zy),c=Tap(i.uv+o.xw),d=Tap(i.uv+o.zw);
        float wa=BloomKaris(a),wb=BloomKaris(b),wc=BloomKaris(c),wd=BloomKaris(d);
        float3 s=(a*wa+b*wb+c*wc+d*wd)/max(wa+wb+wc+wd,0.0001);
        s=BloomPrefilter(s,_BloomCurve.x,_BloomCurve.y,_BloomCurve.z);
        return float4(s,1.0);
    }
    float4 fragDown(v2f_img i):SV_Target{return Box(i.uv,1.0);}
    float4 fragUp(v2f_img i):SV_Target{return Box(i.uv,0.5);}
    float4 fragComposite(v2f_img i):SV_Target
    {
        float2 uv=i.uv;
        #if UNITY_UV_STARTS_AT_TOP
        if(_MainTex_TexelSize.y<0.0)uv.y=1.0-uv.y;
        #endif
        float4 scene=tex2D(_MainTex,i.uv);
        float3 bloom=tex2D(_BloomTex,uv).rgb;
        return float4(scene.rgb+bloom*_BloomCurve.w,scene.a);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragPrefilter
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragDown
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragUp
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragComposite
            #pragma target 3.0
            ENDCG
        }
    }
    Fallback Off
}
