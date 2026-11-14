// Blood splats from the owner's toon effect pack (owner 2026-10-28; Core NativeBloodSplatLook, Runtime
// NativeBloodPresentation): one world-space quad per splat on its surface, the pack's shaded splat texture (alpha: its
// shape; grey: its toon bevel) in the splat's rim and wet-core colours. Vertices carry everything:
//   COLOR      the rim colour (sRGB, lit on the CPU as today's splats) and the splat's opacity
//   TEXCOORD0  xy the texture coordinates, z the kind (0 round, 1 splash, 2 streak)
//   TEXCOORD1  rgb the wet core colour (sRGB, lit), w how wet it still is (1 fresh, 0 dry: the bevel's sheen goes)
// Premultiplied (Blend One OneMinusSrcAlpha), no depth write, scene fog; the runtime queues it where today's splats draw
// (Transparent-60: smoke and glows draw over the floor's blood). Without the bank's textures the runtime keeps today's fans.
Shader "ChipArena/NativeBloodSplat"
{
    Properties { _SplatRound("Round splat",2D)="black"{} _SplatSplash("Splash",2D)="black"{} _SplatStreak("Streak",2D)="black"{} }
    SubShader
    {
        Tags { "Queue"="Transparent-60" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _SplatRound,_SplatSplash,_SplatStreak;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 core:TEXCOORD1;};
            struct v2f {float4 pos:SV_POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 core:TEXCOORD1;UNITY_FOG_COORDS(2)};
            v2f vert(appdata v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.core=v.core;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            // ---- BLOOD SPLAT SHARED BEGIN (HLSL and GLSL: float literals only, no texture reads) ----
            // A splat texel's colour (linear): shade the texture's toon bevel (grey, about 0.78 on the flat), body its coverage
            // a few mips down (high in the splat's thick middle, low on its thin lobes and drops), rim and core the linear
            // colours, wet the sheen. The middle takes the core colour, the lobes and drops the rim's; the bevel shades the
            // rim darker and, while wet, catches a highlight.
            float3 SplatColor(float shade,float body,float3 rim,float3 core,float wet)
            {
                float inner = smoothstep(0.28, 0.72, body);
                float3 base = lerp(rim, core, inner);
                float s = shade / 0.78;
                float3 lit = base * lerp(1.0, s, 0.45 + 0.35 * wet);
                float gloss = saturate((s - 1.08) * 5.0) * wet;
                return lit + base * gloss * 1.6 + float3(0.05, 0.04, 0.04) * gloss;
            }
            // ---- BLOOD SPLAT SHARED END ----
            float4 frag(v2f i):SV_Target
            {
                float kind=i.uv.z;float2 uv=i.uv.xy;float4 t;float body;
                if(kind<0.5){t=tex2D(_SplatRound,uv);body=tex2Dbias(_SplatRound,float4(uv,0,3.5)).a;}
                else if(kind<1.5){t=tex2D(_SplatSplash,uv);body=tex2Dbias(_SplatSplash,float4(uv,0,3.0)).a;}
                else{t=tex2D(_SplatStreak,uv);body=tex2Dbias(_SplatStreak,float4(uv,0,3.0)).a;}
                float a=saturate(t.a*i.color.a);
                float3 c=SplatColor(t.r,body,GammaToLinearSpace(i.color.rgb),GammaToLinearSpace(i.core.rgb),i.core.w);
                #ifdef UNITY_COLORSPACE_GAMMA
                float3 fog=GammaToLinearSpace(unity_FogColor.rgb);
                #else
                float3 fog=unity_FogColor.rgb;
                #endif
                float4 shaded=float4(c,1);UNITY_APPLY_FOG_COLOR(i.fogCoord,shaded,float4(fog,1));c=shaded.rgb;
                #ifdef UNITY_COLORSPACE_GAMMA
                c=LinearToGammaSpace(max(c,0));
                #endif
                return float4(c*a,a);
            }
            ENDCG
        }
    }
    Fallback Off
}
