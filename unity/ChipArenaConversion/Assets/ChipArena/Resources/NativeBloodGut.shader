// The blood pack's flesh lumps among a gib's fragments (owner 2026-11-10; Core NativeBloodGut, Runtime
// NativeBloodPackPresentation, docs/BLOOD-PACK.md): the pack's mesh with its albedo and normal map, opaque, lit per pixel by
// the key light (with its shadow), the ambient and the arena's light block, wet (NativeBloodLight.cginc); it casts a shadow as
// the fragments do. The materials come from the local bake: without it the fragments stay the game's own lumps.
Shader "ChipArena/NativeBloodGut"
{
    Properties
    {
        _MainTex("Albedo",2D)="white"{}
        _BumpMap("Normals",2D)="bump"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Gloss("Wet highlight",Range(0,1))=0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "AutoLight.cginc"
            #include "NativeBloodLight.cginc"
            sampler2D _MainTex,_BumpMap;float4 _Color;float _Gloss;
            struct v2f
            {
                float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 n:TEXCOORD2;float3 t:TEXCOORD3;float3 b:TEXCOORD4;
                float3 soft:TEXCOORD5;float3 domDir:TEXCOORD6;float3 domColor:TEXCOORD7;UNITY_SHADOW_COORDS(8) UNITY_FOG_COORDS(9)
            };
            v2f vert(appdata_tan v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.n=UnityObjectToWorldNormal(v.normal);o.t=UnityObjectToWorldDir(v.tangent.xyz);o.b=cross(o.n,o.t)*v.tangent.w*unity_WorldTransformParams.w;
                o.soft=max(ShadeSH9(float4(o.n,1.0)),0.0)+BloodArenaLights(o.world,o.n,0.35,o.domDir,o.domColor);
                UNITY_TRANSFER_SHADOW(o,float2(0.0,0.0));UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 albedo=tex2D(_MainTex,i.uv).rgb*_Color.rgb;float3 nt=UnpackNormal(tex2D(_BumpMap,i.uv));
                float3 n=normalize(i.t*nt.x+i.b*nt.y+i.n*nt.z);float3 v=normalize(_WorldSpaceCameraPos-i.world);float3 l=_WorldSpaceLightPos0.xyz;
                UNITY_LIGHT_ATTENUATION(atten,i,i.world);
                float3 key=_LightColor0.rgb*(saturate(dot(n,l))*atten);
                float3 col=albedo*(i.soft+key);
                col+=(_LightColor0.rgb*(BloodGlint(n,v,l,_Gloss,0.1)*atten)+i.domColor*BloodGlint(n,v,i.domDir,_Gloss,0.1))*_Gloss;
                #ifdef UNITY_COLORSPACE_GAMMA
                col=LinearToGammaSpace(max(col,0.0));
                #endif
                float4 shaded=float4(col,1.0);UNITY_APPLY_FOG(i.fogCoord,shaded);return shaded;
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct v2f {V2F_SHADOW_CASTER;};
            v2f vert(appdata_base v){v2f o;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o;}
            float4 frag(v2f i):SV_Target{SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    Fallback Off
}
