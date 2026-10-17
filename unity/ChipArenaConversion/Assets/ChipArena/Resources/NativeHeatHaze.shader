Shader "ChipArena/NativeHeatHaze"
{
    // Heat you can see (Claude, 2026-10-16; docs/EXPEDITION-CITY-IRONWORKS.md section 4, gap 2): a rising shimmer over the City
    // Ironworks' hot sources (the furnace mouths, a live runner, the pour, the reheat door, the converters, the slag tongue).
    // Runtime NativeIronworksPresentation draws one dynamic mesh of camera-facing quads (Core NativeIronworksFx.Heat: centre,
    // size, strength) after the opaque world and before the effects; one named GrabPass copies the screen once a frame, and each
    // quad samples it at a small rising offset that fades to its edges (at most 1.2% of the screen at full strength). Depth-tested
    // (whatever stands in front of a source hides its shimmer), no depth write, no blending. The low graphics tier draws none
    // (no grab at all). Per vertex: uv the quad's point (-1..1, y up), colour alpha the strength. Global _HeatClock: the
    // presentation clock.
    Properties {}
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_IronworksHeatGrab" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _IronworksHeatGrab;
            // SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
            float _HeatClock;
            float HeatHash(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
            float HeatNoise(float2 p)
            {
                float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);
                return lerp(lerp(HeatHash(i),HeatHash(i+float2(1.0,0.0)),u.x),lerp(HeatHash(i+float2(0.0,1.0)),HeatHash(i+float2(1.0,1.0)),u.x),u.y);
            }
            // The screen offset (uv units) at a haze quad's point q (-1..1, y up) for its strength and the clock: a shimmer rising
            // through the quad, strongest low in its middle and gone at its edges.
            float2 HeatOffset(float2 q,float strength,float time)
            {
                float fade=saturate(1.0-length(q*float2(1.0,0.8)));fade*=fade*(0.6+0.4*saturate(0.5-q.y*0.5));
                float2 p=q*float2(2.4,1.3)+float2(0.0,-time*1.6);
                float2 n=float2(HeatNoise(p*3.1),HeatNoise(p*3.1+float2(7.3,2.9)))-0.5;
                return n*(0.024*strength*fade);
            }
            // SHARED END
            struct appdata_haze {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float4 grab:TEXCOORD0;float3 q:TEXCOORD1;};
            v2f vert(appdata_haze v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.grab=ComputeGrabScreenPos(o.pos);o.q=float3(v.uv,v.color.a);return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.grab.xy/i.grab.w+HeatOffset(i.q.xy,i.q.z,_HeatClock);
                return tex2D(_IronworksHeatGrab,uv);
            }
            ENDCG
        }
    }
    Fallback Off
}
