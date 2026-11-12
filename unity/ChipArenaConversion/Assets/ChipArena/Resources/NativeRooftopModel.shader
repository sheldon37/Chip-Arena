Shader "ChipArena/NativeRooftopModel"
{
    // The home screen's modelled rooftop (owner 2026-11-11: the menu out of its boxy look; docs/MENU-ROOFTOP-ART.md): the
    // meshes of the local bank ChipArenaStageRooftop (tools/stages/rooftop_art.py on stagekit.py), drawn by Runtime
    // NativeRooftopModels in place of Core NativeRooftopCity's prop quads: our roof, the towers' built detail, the actors'
    // roofs, the screens' frames and the flying cars. One short opaque pass, branch free (the rooftop's passes stay small:
    // DEV_LOG 2026-10-24).
    //  colour  rgb the baked albedo (sRGB), a the baked ambient occlusion
    //  uv0     smoothness, metallic
    //  uv1     the finish's kind and its parameter; kind 11 glows: the albedo times the parameter, unlit (neon tube, lamps,
    //          lit fields), HDR for NativeBloom
    // Lit as the rooftop's props are (NativeRooftopLights.cginc: the street's glow, the screens', signs' and lamps' spill, the
    // night sky) with the moon's key in the anime style's two bands, the sky mirrored in wet and polished faces by their
    // smoothness, a grain on the rough ones. The city fog last. Instanced (the cars).
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            #include "NativeRooftopLights.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;float2 uv0:TEXCOORD0;float2 uv1:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=float4(v.uv0,v.uv1);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);float3 alb=i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                alb=GammaToLinearSpace(alb);
                #endif
                float ao=i.color.a,smooth=i.uv.x,metal=i.uv.y,kind=i.uv.z,gain=i.uv.w;
                float glow=step(10.5,kind)*step(kind,11.5);
                float3 v=normalize(_WorldSpaceCameraPos-i.world);float up=saturate(n.y);float rim=pow(1.0-saturate(dot(n,v)),3.0);
                float3 moonDir=normalize(float3(-.72,.42,.18));float key=saturate(dot(n,moonDir));
                float band=lerp(key,smoothstep(.18,.30,key),step(.5,_ArenaToon.x));
                float occ=lerp(.32,1.0,ao);
                float3 light=(RoofCityGlow(i.world,n)*1.6+float3(.10,.14,.30)*band*.5)*occ+RoofSpill(i.world,n)*lerp(.6,1.0,ao);
                float grain=(RoofNoise(i.world*3.1)*.25+RoofNoise(i.world*.6)*.3)*(1.0-smooth);
                float wet=smoothstep(.5,.95,smooth);
                float3 mirror=RoofMirror(i.world,n,.03+.09*smooth+.25*metal*smooth)*(.3+1.0*wet*up+.5*metal)*occ;
                float3 lit=alb*(.8+grain)*light*2.4+mirror+light*rim*.08*metal;
                float3 c=lerp(lit,alb*gain,glow);
                return float4(RoofFog(c,i.world),1.0);
            }
            ENDCG
        }
        // A depth-only shadow caster: the models in the camera's depth texture (the anime outline reads it); nothing casts.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {V2F_SHADOW_CASTER;};
            v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)return o;}
            float4 frag(v2f i):SV_Target{SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    Fallback Off
}
