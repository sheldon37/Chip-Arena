Shader "ChipArena/MeridianFar"
{
    // Meridian's far layer (Runtime NativeMeridianLandmarksDraw; the MERIDIAN art bible 2.2: from 150 m to 900 m a landmark reads by
    // its emissive parts, fogged to at most about 65%, and its unlit mass is held by the rim). The landmarks' bank meshes
    // (tools/stages/meridian_landmarks.py, stagekit's vertex format): colour.rgb the albedo (sRGB), colour.a the baked occlusion,
    // uv1.y on the glow sub-mesh the emission gain. A plain forward pass: the albedo under the key (wrapped), the look's ambient
    // (the SH) and the arena's rim from behind, then the look's own fog (linear, exp or exp2, Unity's) stopped at _FogCap, so a
    // landmark past the fog's end still shows as a fogged silhouette with its lights. No shadows cast or received: the far layer
    // stands beyond the moon's cascades. _Glow (material): the glow sub-mesh, unlit.
    Properties { _Glow("Glow sub-mesh",Float)=0 _FogCap("Most fog taken",Range(0,1))=0.65 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            float _Glow;float _FogCap;float4 _ArenaRimDir;float4 _ArenaRimColor;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv1:TEXCOORD1;float4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 nW:TEXCOORD1;float4 col:TEXCOORD2;float gain:TEXCOORD3;};
            v2f vert(appdata v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.nW=UnityObjectToWorldNormal(v.normal);
                float3 c=v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                c=GammaToLinearSpace(c);
                #endif
                o.col=float4(c,v.color.a);o.gain=max(v.uv1.y,0.0);return o;
            }
            // The look's fog at a distance (Unity's own formulas), stopped at the cap.
            float FogAmount(float z)
            {
                float f=1.0;
                #if defined(FOG_LINEAR)
                f=saturate(z*unity_FogParams.z+unity_FogParams.w);
                #elif defined(FOG_EXP)
                f=saturate(exp2(-unity_FogParams.y*z));
                #elif defined(FOG_EXP2)
                f=saturate(exp2(-(unity_FogParams.x*z)*(unity_FogParams.x*z)));
                #endif
                return min(1.0-f,_FogCap);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float z=distance(i.world,_WorldSpaceCameraPos);
                float3 col;
                if(_Glow>0.5)col=i.col.rgb*i.gain;
                else
                {
                    float3 n=normalize(i.nW);float3 v=normalize(_WorldSpaceCameraPos-i.world);
                    if(dot(n,v)<0.0)n=-n;
                    float key=saturate((dot(n,_WorldSpaceLightPos0.xyz)+0.3)/1.3);
                    float rim=pow(1.0-saturate(dot(n,v)),3.0)*saturate(dot(n,normalize(_ArenaRimDir.xyz))*0.5+0.5);
                    float3 amb=ShadeSH9(float4(n,1.0));
                    col=i.col.rgb*(amb*i.col.a+_LightColor0.rgb*key*0.8)+_ArenaRimColor.rgb*rim*0.6;
                }
                #if defined(FOG_LINEAR)||defined(FOG_EXP)||defined(FOG_EXP2)
                col=lerp(col,unity_FogColor.rgb,FogAmount(z));
                #endif
                return fixed4(col,1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
