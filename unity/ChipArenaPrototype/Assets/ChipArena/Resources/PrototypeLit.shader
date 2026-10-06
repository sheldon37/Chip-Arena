Shader "ChipArena/PrototypeLit"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 position : SV_POSITION; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.position=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float light=.42+.58*saturate(dot(normalize(i.normal),normalize(float3(-.4,.9,-.3))));
                float rim=pow(1-saturate(dot(normalize(_WorldSpaceCameraPos-i.world),normalize(i.normal))),3)*.18;
                float fog=saturate(length(_WorldSpaceCameraPos-i.world)/90);
                return fixed4(lerp(_Color.rgb*light+rim, float3(.026,.036,.070),fog*.65),1);
            }
            ENDCG
        }
    }
}
