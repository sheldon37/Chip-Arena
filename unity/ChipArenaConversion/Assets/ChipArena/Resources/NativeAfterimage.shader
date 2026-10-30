// The afterimages' pack look (owner 2026-10-28: the motion-trails pack "wherever it fits"; Core
// NativeAfterimages, Runtime NativeWeaponFxPresentation): a copy of a pilot (its voxel parts or the model's baked echo) as a
// stylised motion trail in the pilot's accent. Over the copy's life (_AccentTint.x, 0 new to 1 gone) it reads the pack's
// colour ramp (the global _AfterimageRamp: across it the life, along it streaks, sampled by height and the copy's seed in
// _AccentTint.y) for its brightness, dissolves along the ramp's streaks (read further along it) and fine scanlines with a
// hot front, and swells a little off its surface as it appears, settling as it goes. A fresnel rim keeps its outline.
// _BodyTint.rgb is the accent (linear, HDR), .a the copy's opacity (NativeAfterimages.Fade). Additive, no depth write,
// depth tested against the opaque scene, front faces, scene fog toward black; Transparent+1 as the ghosts. _EchoOffset: the
// model echoes' depth offset toward the eye (as NativeWeaponGhost). Drawn only where the bank holds the ramp.
Shader "ChipArena/NativeAfterimage"
{
    Properties { _BodyTint("Colour (linear) and opacity",Color)=(1,1,1,1) _AccentTint("Life share, seed",Vector)=(0,0,1,1) _EchoOffset("Depth offset",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Offset [_EchoOffset],[_EchoOffset]
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _AfterimageRamp;
            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
            UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
            UNITY_INSTANCING_BUFFER_END(Props)
            // ---- AFTERIMAGE SHARED BEGIN (HLSL and GLSL: float literals only, no texture reads) ----
            // How far (m) the copy stands off its surface at life share u: a slight swell as it appears, settling as it goes.
            float AfterimageSwell(float u)
            {
                return 0.022 * (1.0 - u) * (1.0 - u);
            }
            // facing |N.V|, u the life share, ramp the ramp's brightness at this life (white when new, streaked later), streak
            // its streaks further along (the dissolve's noise), lines the scanlines (0..1). Returns the intensity (x) and the
            // white-hot share (y): a faint body and a strong rim lit by the ramp, dissolving where the streaks and lines fall
            // under a front that rises with u, the front itself hot.
            float2 AfterimageLook(float facing, float u, float ramp, float streak, float lines)
            {
                float rim = pow(saturate(1.0 - facing), 2.4);
                float body = 0.1 + 1.5 * rim;
                float n = saturate((streak - 0.12) * 2.2) * 0.8 + lines * 0.2;
                float front = u * 1.25 - 0.3;
                float keep = smoothstep(front, front + 0.1, n);
                float edge = keep * (1.0 - smoothstep(front + 0.1, front + 0.24, n)) * smoothstep(0.05, 0.2, u);
                float shine = 0.65 + 0.7 * ramp;
                return float2(body * shine * keep + edge * 1.1, saturate(edge * 0.45 + (1.0 - u) * (1.0 - u) * 0.3));
            }
            // ---- AFTERIMAGE SHARED END ----
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;float3 world:TEXCOORD2;UNITY_FOG_COORDS(3) UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
                float u=saturate(UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint).x);float3 n=UnityObjectToWorldNormal(v.normal);
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz+n*AfterimageSwell(u);
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1.0));o.normal=n;o.view=_WorldSpaceCameraPos-world;o.world=world;
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);float4 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);float4 life=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint);
                float u=saturate(life.x),facing=abs(dot(normalize(i.normal),normalize(i.view)));
                float3 r=tex2D(_AfterimageRamp,float2(u*0.96+0.02,frac(i.world.y*0.37+life.y))).rgb,s=tex2D(_AfterimageRamp,float2(0.8,frac(i.world.y*0.61+life.y*1.7))).rgb;
                float lines=0.5+0.5*sin(i.world.y*105.0-u*12.0);
                float2 k=AfterimageLook(facing,u,dot(r,float3(0.3,0.55,0.15)),dot(s,float3(0.3,0.55,0.15)),lines);float m=max(tint.r,max(tint.g,tint.b));
                float4 c=float4(lerp(tint.rgb,float3(m,m,m),k.y)*(k.x*tint.a),0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(0,0,0,0));
                #ifdef UNITY_COLORSPACE_GAMMA
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
